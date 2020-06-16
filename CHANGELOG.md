# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

- Nothing!

[Unreleased]: https://github.com/olivierlacan/keep-a-changelog/compare/v0.3...HEAD

## [0.3] - 2020-06-16
### Changed

- Re-worked internals, now using [FiniteStateMachine] as an abstraction layer.

[0.3]: https://github.com/keyspace/TunnelWorm/compare/v0.2...v0.3
[FiniteStateMachine]: https://github.com/keyspace/SE-Script-FiniteStateMachine

## [0.2] - 2020-05-19
### Changed

- Fixed known edge cases: landing gears or pistons getting stuck. This introduces
  new states to handle the edge-cases, and is not necessarily backward-compatible.
- Added minimal documentation.

[0.2]: https://github.com/keyspace/TunnelWorm/compare/v0.1...v0.2

## [0.1] - 2020-05-17
### Added
- Initial implementation using a switch - as first released to Steam workshop and Reddit.

[0.1]: https://github.com/keyspace/TunnelWorm/releases/tag/v0.1

