# Manager download packages

These versioned ZIPs are byte-for-byte copies of the corresponding GitHub Release
assets. They provide a GitHub-hosted download route for installed Managers when
`github.com/.../releases/download/...` returns a gateway timeout.

The update manifest pins URLs to the commit containing the verified ZIPs on
`raw.githubusercontent.com`. No Manager reinstall is required. Publish and verify
the ZIP commit first, then update the manifest; never replace an existing version's
files with different bytes.

Tools 2.4.29 SHA-256:

| Package | SHA-256 |
| --- | --- |
| bimboclub_net48.zip | `59661e85bcb8d8becc55db0e3beafb23435df4778a2e10fe7157812aa81938f0` |
| bimboclub_net8.zip | `0d117d5c35a6bd55759b9ff25b224b17756708a4b7aa2cdb4ccb4e6727ac7b13` |

Tools 2.4.28 SHA-256:

| Package | SHA-256 |
| --- | --- |
| bimboclub_net48.zip | `696e725d91f267facb2a75b38e186d519a6fd89c8f5dd68e199f8d2fbb7b4139` |
| bimboclub_net8.zip | `fb5e37e0411df9584722408ec6a70860286735366529bc3319f9d6f7f0d583a6` |

Tools 2.4.27 SHA-256:

| Package | SHA-256 |
| --- | --- |
| bimboclub_net48.zip | `6b2a923a16026acc610401de154d70925f59c594e95581124ca8ad137f382d44` |
| bimboclub_net8.zip | `c0a84d6d1d701c4b26e4922062205b7dd3eedea9b358743804ad44041af34631` |
