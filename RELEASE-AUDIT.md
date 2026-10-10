# Release audit — October 9, 2026

This historical audit describes version 0.3.5.1, which was not a launcher-only package. See release/STATUS.md for the current 0.5.0.0 candidate and unresolved release items. No binary or game content is included in this repository.

## Package examined

- File: `SpiderManInstaller_0.3.5.1_x64.msix`
- Size: 1,026,023,138 bytes
- SHA-256: `8df5ade3b15468a7d1147b0bfb615d12d68eec65d232c8a0c160238b29e523ca`
- Internal identity: `Local.SpiderManLauncher`, version `0.3.5.1`
- Entries inventoried: 89

## Findings

1. The package includes 12 modified game asset archives, totalling 1,197,106,123 uncompressed bytes, alongside modified indexes and compatibility binaries. These are not merely transformation instructions. A launcher-only distribution needs to derive the required content from each user's owned copy instead of bundling these archives. Simply deleting the archives would break preparation.
2. No license or third-party-notice files are bundled in the MSIX. Existing local notices incorrectly state that no game assets or compatibility modules are distributed and must be corrected.
3. Adapted DepotDownloader code is compiled into the launcher. Its GPL-2.0 terms require a corresponding-source distribution plan; distributing a binary alone without addressing these terms is not a completed release plan. SteamKit's LGPL obligations and the other dependency licenses must also be addressed. This does not imply uploading unrelated port research or proprietary game source.
4. Compatibility DLLs, executable components, patch data, seed preferences, and artwork need provenance and distribution review. File names and hashes do not establish redistribution rights.

Primary dependency license: [DepotDownloader at the pinned commit](https://github.com/SteamRE/DepotDownloader/blob/ec692a8d9eaa9da4d966287e14fbc76d8ebe1081/LICENSE).

## Credential screening

Filename and readable-string checks detected no private-key containers, credential caches, log/dump files, private-key blocks, GitHub tokens, JWTs, or quoted credential assignments. This was a heuristic screening, not a complete binary security audit or proof that every byte is free of secrets. Opaque asset archives were inventoried separately.

Never upload private signing keys, account credentials, user saves, or diagnostic captures. Only a public signing certificate may be included where appropriate.

## Remaining release work

- Produce a separate launcher-only distribution without bundled proprietary assets.
- Implement and test replacement preparation steps against a user's owned content.
- Review compatibility component origins and licenses.
- Include accurate notices, license texts, and required corresponding-source materials.
- Rebuild, sign, test, and audit the new package before uploading it.

The working local and console builds are not changed by this repository preparation.
