#include <windows.h>

#include <algorithm>
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <memory>
#include <stdexcept>
#include <string>
#include <vector>

#include <usvfs.h>

namespace fs = std::filesystem;

namespace
{
constexpr std::uint32_t ConfigMagic = 0x31534656; // VFS1

struct Mapping
{
  std::uint32_t kind{};
  std::wstring source;
  std::wstring destination;
  unsigned int flags{};
};

struct Config
{
  std::string instanceName;
  std::wstring executable;
  std::wstring arguments;
  std::wstring workingDirectory;
  std::vector<Mapping> mappings;
};

std::uint32_t readUInt32(std::ifstream& input)
{
  std::uint32_t value{};
  input.read(reinterpret_cast<char*>(&value), sizeof(value));
  if (!input) {
    throw std::runtime_error("Unexpected end of USVFS host configuration");
  }

  return value;
}

std::string readUtf8(std::ifstream& input)
{
  const auto size = readUInt32(input);
  if (size > 16 * 1024 * 1024) {
    throw std::runtime_error("Invalid string size in USVFS host configuration");
  }

  std::string value(size, '\0');
  input.read(value.data(), static_cast<std::streamsize>(size));
  if (!input) {
    throw std::runtime_error("Unexpected end of USVFS host configuration string");
  }

  return value;
}

std::wstring fromUtf8(const std::string& value)
{
  if (value.empty()) {
    return {};
  }

  const int size = MultiByteToWideChar(
      CP_UTF8, MB_ERR_INVALID_CHARS, value.data(), static_cast<int>(value.size()), nullptr, 0);
  if (size <= 0) {
    throw std::runtime_error("Invalid UTF-8 in USVFS host configuration");
  }

  std::wstring result(size, L'\0');
  MultiByteToWideChar(
      CP_UTF8, MB_ERR_INVALID_CHARS, value.data(), static_cast<int>(value.size()), result.data(), size);
  return result;
}

Config readConfig(const fs::path& path)
{
  std::ifstream input(path, std::ios::binary);
  if (!input) {
    throw std::runtime_error("Failed to open USVFS host configuration");
  }

  if (readUInt32(input) != ConfigMagic) {
    throw std::runtime_error("Unsupported USVFS host configuration format");
  }

  Config config;
  config.instanceName = readUtf8(input);
  config.executable = fromUtf8(readUtf8(input));
  config.arguments = fromUtf8(readUtf8(input));
  config.workingDirectory = fromUtf8(readUtf8(input));

  const auto mappingCount = readUInt32(input);
  if (mappingCount > 100000) {
    throw std::runtime_error("Invalid mapping count in USVFS host configuration");
  }

  config.mappings.reserve(mappingCount);
  for (std::uint32_t index = 0; index < mappingCount; ++index) {
    Mapping mapping;
    mapping.kind = readUInt32(input);
    mapping.flags = readUInt32(input);
    mapping.source = fromUtf8(readUtf8(input));
    mapping.destination = fromUtf8(readUtf8(input));
    config.mappings.push_back(std::move(mapping));
  }

  return config;
}

std::wstring quote(const std::wstring& value)
{
  std::wstring result = L"\"";
  std::size_t backslashes = 0;
  for (const wchar_t character : value) {
    if (character == L'\\') {
      ++backslashes;
      continue;
    }

    if (character == L'\"') {
      result.append(backslashes * 2 + 1, L'\\');
      result.push_back(character);
      backslashes = 0;
      continue;
    }

    result.append(backslashes, L'\\');
    backslashes = 0;
    result.push_back(character);
  }

  result.append(backslashes * 2, L'\\');
  result.push_back(L'\"');
  return result;
}

void waitForVfsProcessTree()
{
  // Launchers such as Gunslinger Play.exe exit immediately after spawning the
  // actual game. Keep the controller alive until every hooked descendant exits.
  constexpr int requiredEmptyPolls = 10;
  int emptyPolls = 0;
  while (emptyPolls < requiredEmptyPolls) {
    std::size_t processCount = 0;
    if (!usvfsGetVFSProcessList(&processCount, nullptr)) {
      throw std::runtime_error(
          "usvfsGetVFSProcessList failed: " + std::to_string(GetLastError()));
    }

    emptyPolls = processCount == 0 ? emptyPolls + 1 : 0;
    Sleep(100);
  }
}
}

int wmain(int argc, wchar_t* argv[])
{
  if (argc != 2) {
    std::cerr << "Usage: StalkerModLauncher.UsvfsX86Host.exe <configuration>\n";
    return 10;
  }

  try {
    const auto config = readConfig(argv[1]);
    std::unique_ptr<usvfsParameters, decltype(&usvfsFreeParameters)> parameters(
        usvfsCreateParameters(), &usvfsFreeParameters);
    if (!parameters) {
      throw std::runtime_error("usvfsCreateParameters returned null");
    }

    usvfsSetInstanceName(parameters.get(), config.instanceName.c_str());
    usvfsSetDebugMode(parameters.get(), false);
    usvfsSetLogLevel(parameters.get(), LogLevel::Info);
    usvfsSetCrashDumpType(parameters.get(), CrashDumpsType::None);
    usvfsSetCrashDumpPath(parameters.get(), "");
    usvfsInitLogging(false);

    if (!usvfsCreateVFS(parameters.get())) {
      throw std::runtime_error("usvfsCreateVFS failed");
    }

    usvfsClearVirtualMappings();
    for (const auto& mapping : config.mappings) {
      const BOOL mapped = mapping.kind == 0
          ? usvfsVirtualLinkDirectoryStatic(
                mapping.source.c_str(), mapping.destination.c_str(), mapping.flags)
          : usvfsVirtualLinkFile(
                mapping.source.c_str(), mapping.destination.c_str(), mapping.flags);
      if (!mapped) {
        throw std::runtime_error("USVFS x86 host mapping failed");
      }
    }

    std::wstring commandLine = quote(config.executable);
    if (!config.arguments.empty()) {
      commandLine += L" " + config.arguments;
    }

    std::vector<wchar_t> commandBuffer(commandLine.begin(), commandLine.end());
    commandBuffer.push_back(L'\0');
    STARTUPINFOW startup{};
    startup.cb = sizeof(startup);
    PROCESS_INFORMATION process{};
    if (!usvfsCreateProcessHooked(
            config.executable.c_str(), commandBuffer.data(), nullptr, nullptr, FALSE, 0,
            nullptr, config.workingDirectory.c_str(), &startup, &process)) {
      throw std::runtime_error("usvfsCreateProcessHooked failed: " + std::to_string(GetLastError()));
    }

    WaitForSingleObject(process.hProcess, INFINITE);
    DWORD exitCode = 1;
    GetExitCodeProcess(process.hProcess, &exitCode);
    CloseHandle(process.hThread);
    CloseHandle(process.hProcess);
    waitForVfsProcessTree();
    usvfsDisconnectVFS();
    return static_cast<int>(exitCode);
  } catch (const std::exception& exception) {
    std::cerr << "USVFS x86 host failed: " << exception.what() << "\n";
    usvfsDisconnectVFS();
    return 20;
  }
}
