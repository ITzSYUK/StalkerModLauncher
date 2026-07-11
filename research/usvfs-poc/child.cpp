#include <filesystem>
#include <fstream>
#include <iostream>
#include <string>
#include <windows.h>

namespace fs = std::filesystem;

static std::string readText(const fs::path& path)
{
  std::ifstream input(path, std::ios::binary);
  if (!input) {
    return "<missing>";
  }

  return {std::istreambuf_iterator<char>(input), std::istreambuf_iterator<char>()};
}

int wmain(int argc, wchar_t* argv[])
{
  if (argc != 3) {
    std::wcerr << L"Usage: usvfs_overlay_child <virtual-root> <result-file>\n";
    return 10;
  }

  const fs::path root = argv[1];
  const fs::path outputPath = argv[2];

  Sleep(500);

  std::ofstream output(outputPath, std::ios::binary);
  if (!output) {
    std::wcerr << L"Failed to open result file: " << outputPath.wstring() << L"\n";
    return 11;
  }

  output << "shared=" << readText(root / "shared.txt") << "\n";
  output << "base-only=" << readText(root / "base-only.txt") << "\n";
  output << "mod-only=" << readText(root / "mod-only.txt") << "\n";
  output << "nested=" << readText(root / "gamedata" / "config" / "system.ltx") << "\n";
  output << "bootstrap=" << readText(root / "physical-bootstrap.txt") << "\n";
  output << "profile-file=" << readText(root / "fsgame.ltx") << "\n";

  return 0;
}
