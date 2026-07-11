#include <windows.h>

#include <string>
#include <vector>

namespace
{
std::wstring quote(const std::wstring& value)
{
  return L"\"" + value + L"\"";
}
}

int wmain(int argc, wchar_t* argv[])
{
  if (argc != 4) {
    return 10;
  }

  std::wstring commandLine = quote(argv[1]) + L" " + quote(argv[2]) + L" " + quote(argv[3]);
  std::vector<wchar_t> buffer(commandLine.begin(), commandLine.end());
  buffer.push_back(L'\0');

  STARTUPINFOW startup{};
  startup.cb = sizeof(startup);
  PROCESS_INFORMATION process{};
  if (!CreateProcessW(
          argv[1], buffer.data(), nullptr, nullptr, FALSE, 0, nullptr, nullptr,
          &startup, &process)) {
    return 11;
  }

  CloseHandle(process.hThread);
  CloseHandle(process.hProcess);
  return 0;
}
