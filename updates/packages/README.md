# Manager download packages

These versioned ZIPs are byte-for-byte copies of the corresponding GitHub Release
assets. They provide a GitHub-hosted download route for installed Managers when
`github.com/.../releases/download/...` returns a gateway timeout.

The update manifest pins URLs to the commit containing the verified ZIPs on
`raw.githubusercontent.com`. No Manager reinstall is required. Publish and verify
the ZIP commit first, then update the manifest; never replace an existing version's
files with different bytes.

Tools 2.4.27 SHA-256:

| Package | SHA-256 |
| --- | --- |
| bimboclub_net48.zip | `6b2a923a16026acc610401de154d70925f59c594e95581124ca8ad137f382d44` |
| bimboclub_net8.zip | `c0a84d6d1d701c4b26e4922062205b7dd3eedea9b358743804ad44041af34631` |
