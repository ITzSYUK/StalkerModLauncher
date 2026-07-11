#include <windows.h>

#include <filesystem>
#include <fstream>
#include <iostream>
#include <memory>
#include <string>
#include <vector>

#include <usvfs.h>

namespace fs = std::filesystem;

static std::wstring quote(const fs::path& value)
{
  return L"\"" + value.wstring() + L"\"";
}

static void writeText(const fs::path& path, const std::string& value)
{
  fs::create_directories(path.parent_path());

  std::ofstream output(path, std::ios::binary);
  if (!output) {
    throw std::runtime_error("Failed to write test file");
  }

  output << value;
}

static std::string readText(const fs::path& path)
{
  std::ifstream input(path, std::ios::binary);
  if (!input) {
    return {};
  }

  return {std::istreambuf_iterator<char>(input), std::istreambuf_iterator<char>()};
}

static fs::path getExecutableDirectory()
{
  std::wstring buffer(MAX_PATH, L'\0');
  DWORD length = GetModuleFileNameW(nullptr, buffer.data(), static_cast<DWORD>(buffer.size()));

  while (length == buffer.size()) {
    buffer.resize(buffer.size() * 2);
    length = GetModuleFileNameW(nullptr, buffer.data(), static_cast<DWORD>(buffer.size()));
  }

  if (length == 0) {
    throw std::runtime_error("GetModuleFileNameW failed");
  }

  buffer.resize(length);
  return fs::path(buffer).parent_path();
}

static fs::path createPocRoot()
{
  std::wstring temp(MAX_PATH, L'\0');
  const DWORD length = GetTempPathW(static_cast<DWORD>(temp.size()), temp.data());
  if (length == 0 || length > temp.size()) {
    throw std::runtime_error("GetTempPathW failed");
  }

  temp.resize(length);
  fs::path root = fs::path(temp) / ("stalker-usvfs-poc-" + std::to_string(GetCurrentProcessId()));

  std::error_code ignored;
  fs::remove_all(root, ignored);
  fs::create_directories(root);
  return root;
}

int wmain()
{
  try {
    const fs::path root = createPocRoot();
    const fs::path base = root / "base";
    const fs::path mod = root / "mod";
    const fs::path virtualRoot = root / "virtual-root";
    const fs::path result = root / "result.txt";

    fs::create_directories(virtualRoot);

    writeText(base / "shared.txt", "base");
    writeText(base / "base-only.txt", "base");
    writeText(base / "gamedata" / "config" / "system.ltx", "base-system");

    writeText(mod / "shared.txt", "mod");
    writeText(mod / "mod-only.txt", "mod");
    writeText(mod / "gamedata" / "config" / "system.ltx", "mod-system");

    std::unique_ptr<usvfsParameters, decltype(&usvfsFreeParameters)> parameters(
        usvfsCreateParameters(), &usvfsFreeParameters);
    usvfsSetInstanceName(parameters.get(), "stalker_launcher_usvfs_poc");
    usvfsSetDebugMode(parameters.get(), false);
    usvfsSetLogLevel(parameters.get(), LogLevel::Debug);
    usvfsSetCrashDumpType(parameters.get(), CrashDumpsType::None);
    usvfsSetCrashDumpPath(parameters.get(), "");

    usvfsInitLogging(false);
    if (!usvfsCreateVFS(parameters.get())) {
      std::cerr << "usvfsCreateVFS failed\n";
      return 20;
    }

    usvfsClearVirtualMappings();

    if (!usvfsVirtualLinkDirectoryStatic(base.c_str(), virtualRoot.c_str(), LINKFLAG_RECURSIVE)) {
      std::wcerr << L"Failed to map base: " << base.wstring() << L"\n";
      return 21;
    }

    if (!usvfsVirtualLinkDirectoryStatic(mod.c_str(), virtualRoot.c_str(), LINKFLAG_RECURSIVE)) {
      std::wcerr << L"Failed to map mod: " << mod.wstring() << L"\n";
      return 22;
    }

    const fs::path child = getExecutableDirectory() / "usvfs_overlay_child.exe";
    std::wstring command = quote(child) + L" " + quote(virtualRoot) + L" " + quote(result);

    STARTUPINFOW startup{};
    startup.cb = sizeof(startup);
    PROCESS_INFORMATION process{};

    std::vector<wchar_t> commandBuffer(command.begin(), command.end());
    commandBuffer.push_back(L'\0');

    if (!usvfsCreateProcessHooked(nullptr, commandBuffer.data(), nullptr, nullptr, FALSE, 0,
                                  nullptr, getExecutableDirectory().c_str(), &startup,
                                  &process)) {
      std::cerr << "usvfsCreateProcessHooked failed. GetLastError=" << GetLastError() << "\n";
      return 23;
    }

    WaitForSingleObject(process.hProcess, INFINITE);

    DWORD exitCode = 99;
    GetExitCodeProcess(process.hProcess, &exitCode);
    CloseHandle(process.hThread);
    CloseHandle(process.hProcess);

    const std::string output = readText(result);
    std::cout << output;

    const bool success = exitCode == 0 && output.find("shared=mod") != std::string::npos &&
                         output.find("base-only=base") != std::string::npos &&
                         output.find("mod-only=mod") != std::string::npos &&
                         output.find("nested=mod-system") != std::string::npos;

    usvfsDisconnectVFS();

    if (!success) {
      std::cerr << "USVFS overlay PoC failed. Child exit code: " << exitCode << "\n";
      std::wcerr << L"PoC files: " << root.wstring() << L"\n";
      return 30;
    }

    std::wcout << L"USVFS overlay PoC passed. Files: " << root.wstring() << L"\n";
    return 0;
  } catch (const std::exception& exception) {
    std::cerr << "USVFS overlay PoC error: " << exception.what() << "\n";
    return 1;
  }
}
