@{
    SchemaVersion = 1
    SourceRepository = 'https://github.com/ModOrganizer2/usvfs'
    SourceRevision = '57f1ea5e6ad13f7435a7af184748e6c1312c5637'
    SourcePatch = 'scripts\patches\usvfs-msvc-pch.patch'
    RuntimeVersion = '0.5.7.2'
    Files = @(
        @{
            Name = 'usvfs_x64.dll'
            RelativePath = 'lib\usvfs_x64.dll'
            Sha256 = 'D70ED1F903D2394E9486FAEE7413E035EBED05FEF3411300ABDBD35E4D21C255'
        }
        @{
            Name = 'usvfs_proxy_x64.exe'
            RelativePath = 'bin\usvfs_proxy_x64.exe'
            Sha256 = '35A6AA7ED8CB2E0DEE32E00A26D8175732858762CDC33B25BC6250C2EC02B11B'
        }
        @{
            Name = 'usvfs_x86.dll'
            RelativePath = 'lib\usvfs_x86.dll'
            Sha256 = '72686B11AD6804482FC30540C6C0FABDFB9D94FCB75E3E5D02264C850887C069'
        }
        @{
            Name = 'usvfs_proxy_x86.exe'
            RelativePath = 'bin\usvfs_proxy_x86.exe'
            Sha256 = '8487D49C428293F40B4B2B0CB3C06B5C5676FB30461D8C3A0F5E297C91EBB5EB'
        }
    )
}
