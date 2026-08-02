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
            Sha256 = '859B270437006DEF03934775B606A687D68EB1CD7EA9DE39D7F35E1918FB52F4'
        }
        @{
            Name = 'usvfs_proxy_x64.exe'
            RelativePath = 'bin\usvfs_proxy_x64.exe'
            Sha256 = '346A5903A1F434AEB93B288397FC6688F8387827FD9D6BF1DDD64A8465E66B16'
        }
        @{
            Name = 'usvfs_x86.dll'
            RelativePath = 'lib\usvfs_x86.dll'
            Sha256 = '01566E6FC327E1E8354BB1E704C735FF94FBA9E07B066C6D404251C63FAAF01C'
        }
        @{
            Name = 'usvfs_proxy_x86.exe'
            RelativePath = 'bin\usvfs_proxy_x86.exe'
            Sha256 = '57F1EBDF226077029E4386E15A5F788298FD68A5DB90AFC8192262120FCA77C2'
        }
    )
}
