# Zydis Generator

The instruction database and code generator behind [Zydis](https://github.com/zyantific/zydis). The data files in this
repository are the single source of truth for every instruction Zydis can decode, encode, and format. The generator
turns them into the C tables and enums that are committed into the Zydis repository under `src/Generated/` and
`include/Zydis/Generated/`.

## Repository layout

| Path         | Contents                                                                            |
| ------------ | ----------------------------------------------------------------------------------- |
| `assets/`    | Project logo.                                                                       |
| `datafiles/` | The instruction database and supporting data files.                                 |
| `scripts/`   | Python utilities that exchange data with a Zydis checkout.                          |
| `src/`       | The .NET solution: generator CLI, core library, source generators, and test suite.  |

## Data files

| File                      | Contents                                                                                     |
| ------------------------- | -------------------------------------------------------------------------------------------- |
| `asz_maps.json`           | Reference data for the address-size map values used by `adsize_map`.                         |
| `decoder_tree_nodes.json` | Decision-node (filter) definitions: node names, slot counts, and slot-value syntax. Compiled into C# node types by `Zydis.Generator.SourceGenerator` at build time. |
| `formatter_strings.json`  | String tables for the Zydis formatter, emitted as `FormatterStrings.inc`.                    |
| `instructions.json`       | The instruction database itself: one JSON object per instruction form (more than 10,000 definitions). |
| `osz_maps.json`           | Reference data for the operand-size map values used by `opsize_map`.                         |
| `registers.json`          | Register definitions, consumed by `scripts/gen_registers.py`.                                |
| `shared_enums.json`       | Enum definitions shared between the generator and Zydis, compiled into C# enums by `Zydis.Generator.Enums.SourceGenerator` at build time. |

## The instruction database

`datafiles/instructions.json` is a JSON array with one object per instruction form:

```json
{
  "mnemonic": "adc",
  "opcode": "10",
  "filters": [
    { "filter": "modrm_mod", "value": "3" }
  ],
  "operands": [
    { "action": "read_write", "operand_type": "gpr8", "encoding": "modrm_rm" },
    { "operand_type": "gpr8", "encoding": "modrm_reg" }
  ],
  "opsize_map": "byteop",
  "meta_info": { "category": "BINARY", "extension": "BASE", "isa_set": "I86" },
  "affected_flags": { "access": "must_write", "cf": "t_m", "pf": "m", "af": "m", "zf": "m", "sf": "m", "of": "m" }
}
```

The main fields:

| Field                              | Meaning                                                                             |
| ---------------------------------- | ----------------------------------------------------------------------------------- |
| `affected_flags`                   | How the instruction reads and writes the CPU flags.                                 |
| `annotations`                      | Free-form structured annotations.                                                   |
| `comment`                          | Provenance text carried over from the original data import (base64-encoded).        |
| `cpl`                              | Required privilege level.                                                           |
| `encoding`                         | Encoding space (legacy, `3dnow`, `xop`, `vex`, `evex`, `mvex`).                     |
| `evex` / `mvex` / `vex`            | Encoding-specific properties (tuple type, element size, masking, and similar).      |
| `filters`                          | Decode-time tests that select this form (see below).                                |
| `flags`                            | Miscellaneous instruction flags.                                                    |
| `force_modrm_reg` / `force_modrm_rm` | Informational ModRM flags carried over from the data import.                      |
| `meta_info`                        | Category, ISA set, and ISA extension (emitted into the generated meta tables).      |
| `mnemonic`                         | Instruction mnemonic; also feeds the generated mnemonic enum.                       |
| `opcode` / `opcode_map`            | Opcode byte and opcode map (`0f`, `0f38`, `map4`, ...).                             |
| `operands`                         | Ordered operand list: type, encoding, access, element type, and widths.             |
| `opsize_map` / `adsize_map`        | Operand-size and address-size scaling behavior.                                     |
| `prefix_flags`                     | Accepted prefixes (`LOCK`, `XACQUIRE`, and similar).                                |

### Filters

Filters are the decode-time tests that distinguish instruction forms sharing an opcode: processor mode, ModRM fields,
mandatory prefix, REX/VEX/EVEX bits, and so on. The full list of filters and their accepted values is defined in
`datafiles/decoder_tree_nodes.json`. Values support negation with a `!` prefix (for example `"modrm_mod": "!3"`).

`"filters"` is an ordered array of `{ "filter": ..., "value": ... }` entries. The entry order is meaningful: it records
the order in which the generated decoder tree tests the filters for that definition, as chosen by the decoder-tree
optimizer. This makes filter-order changes visible in data diffs instead of only in regenerated table output. An array
schema is used (rather than a JSON object whose member order carries the same information) because RFC 8259 leaves
object member order insignificant and tooling may not preserve it. The reader still accepts the object form for
compatibility; writes always emit the array form. Duplicate filters within one definition are rejected at parse time.

Because the recorded order is derived data, it can drift when definitions are added or changed. The `lint` CLI mode
reports drift, and the `migrate-order` mode rewrites the data with the current optimizer-chosen order (see below).

## Decoder-tree construction

The decoder tree is built by `Zydis.Generator.Core`'s variable-position builder. Definitions are bucketed by encoding,
opcode map, and opcode byte; each bucket's members are validated (their filter regions must be non-overlapping and
unambiguous) and handed to a dynamic-programming tree constructor that searches filter orders per subtree and keeps
the cheapest result. Constructed subtrees are interned so identical subtrees are stored once, and the table emitter
lays the result out as a DAG in two passes. The output is deterministic: regenerating twice produces byte-identical
files, and CI enforces this.

A frozen reference implementation of the fixed-filter-order builder is kept alongside for verification: the `verify`
CLI mode proves region equivalence between both builders' outputs for every opcode table.

## Generator CLI

`src/Zydis.Generator` is the command-line entry point:

```
dotnet run --project src/Zydis.Generator -- [--tree=<mode>] <arguments>
```

Table generation and verification:

| Mode               | Arguments                     | Effect                                                          |
| ------------------ | ----------------------------- | --------------------------------------------------------------- |
| `dp` (default)     | `<datafiles> <zydis>`         | Generates all tables and enums into a Zydis checkout and prints per-table construction statistics. |
| `verify`           | `<datafiles> [<zydis>]`       | Verifies region equivalence between the variable-position tree and the frozen fixed-order reference, and prints a size comparison. Writes no files. |

Data maintenance:

| Mode             | Arguments      | Effect                                                                         |
| ---------------- | -------------- | ------------------------------------------------------------------------------ |
| `lint`           | `<datafiles>`  | Reports definitions whose recorded filter order differs from the current optimizer-chosen order. Advisory: always exits 0. |
| `migrate-order`  | `<datafiles>`  | Rewrites `instructions.json` so every definition's filter order matches the optimizer-chosen order, and prints how many definitions changed on disk. Idempotent: re-running on already-migrated data is a byte-for-byte no-op. |
| `migrate-schema` | `<datafiles>`  | Reserializes `instructions.json` in the canonical array filter schema.         |
| `scan-redundant` | `<datafiles>`  | Reports definitions whose filter region is a strict subset of an otherwise identical sibling's, i.e. rows that add no decoding value. Advisory: always exits 0. |

### Arguments

| Argument       | Used by                                   | Description                                                |
| -------------- | ----------------------------------------- | ---------------------------------------------------------- |
| `--tree=<mode>` | all modes                                | Selects the operating mode: `dp`, `lint`, `migrate-order`, `migrate-schema`, `scan-redundant`, or `verify`. Optional; defaults to `dp`. An unknown value prints the mode list and exits with code 1. |
| `<datafiles>`  | all modes                                 | Path to this repository's `datafiles/` directory. `instructions.json` is read from it in every mode (`dp` also reads `formatter_strings.json`), and the `migrate-*` modes rewrite `instructions.json` inside it in place. |
| `<zydis>`      | `dp` (required), `verify` (optional)      | Path to a Zydis checkout, or any directory containing `src/Generated/` and `include/Zydis/Generated/`. The directory must already exist; generated files are overwritten in place. `verify` accepts the argument but ignores it, so both modes can be invoked with the same argument list. |

All arguments other than `--tree=<mode>` are positional and mode-specific; a wrong argument count prints the mode's
usage line and exits with code 1.

### Exit codes

| Code | Meaning                                                                                              |
| ---- | ---------------------------------------------------------------------------------------------------- |
| 0    | Success. `lint` and `scan-redundant` also exit 0 when they report findings, since both are advisory.  |
| 1    | Usage error: unknown `--tree` value or wrong number of positional arguments.                          |
| 2    | `verify` only: region differences between the two builders were found.                                |

### Generated output

The `dp` mode writes into a Zydis checkout:

- `include/Zydis/Generated/`: `Enum<Name>.h` enum definitions for instruction categories, ISA extensions, ISA sets,
  and mnemonics.
- `src/Generated/`: the matching `Enum<Name>.inc` string tables, plus:

| File                      | Contents                                          |
| ------------------------- | ------------------------------------------------- |
| `AccessedFlags.inc`       | Deduplicated CPU-flag access records.             |
| `DecoderTables.inc`       | Decoder decision-tree tables and opcode-table lookup. |
| `EncoderTables.inc`       | Encoder definition lookup tables.                 |
| `FormatterStrings.inc`    | Formatter string tables.                          |
| `GetCcInfo.inc`           | Condition-code information lookup.                |
| `GetRelInfo.inc`          | Relative-branch information lookup.               |
| `InstructionDefinitions.inc` | Per-instruction definition records.            |
| `InstructionEncodings.inc`   | Deduplicated instruction-encoding records.     |
| `OperandDefinitions.inc`     | Deduplicated operand records.                  |

## Projects

| Project                                | Purpose                                                                    |
| -------------------------------------- | -------------------------------------------------------------------------- |
| `Zydis.Generator`                      | Command-line entry point wrapping the core library's modes.                |
| `Zydis.Generator.Core`                 | Definition model, JSON serialization, decoder-tree builder and optimizer, and table emitters. |
| `Zydis.Generator.Core.Tests`           | xUnit test suite.                                                          |
| `Zydis.Generator.Enums`                | Hosts the compile-time generated shared enums. Kept as a separate project so the shared-enum source generator runs before the `System.Text.Json` source generator. |
| `Zydis.Generator.Enums.SourceGenerator` | Roslyn source generator that turns `shared_enums.json` into C# enums.     |
| `Zydis.Generator.SourceGenerator`      | Roslyn source generator that turns `decoder_tree_nodes.json` into decision-node types. |
| `Zydis.SourceGeneration`               | Shared helpers for the Roslyn source generators.                           |

## Building and testing

The solution targets the .NET SDK version pinned in `global.json` and uses locked NuGet restore
(`packages.lock.json`).

```
dotnet build
dotnet test
```

## Continuous integration

CI builds the solution, runs the test suite, and enforces two gates on every pull request:

- Decoder-table determinism: the `dp` mode is run twice into separate directories and the outputs are diffed; any
  difference fails the build.
- Filter-order lint: the `lint` mode runs as an advisory step; findings are reported without failing the build.

## Maintaining the instruction data

The typical workflow for adding or changing instruction definitions:

1. Edit `datafiles/instructions.json`.
2. Run `--tree=lint datafiles` to see which definitions' recorded filter orders drifted (new definitions plus any
   existing siblings whose optimal order shifted).
3. Run `--tree=migrate-order datafiles` to rewrite the data with the current optimizer-chosen order.
4. Run `--tree=verify datafiles` to confirm region equivalence, and optionally `--tree=scan-redundant datafiles` to
   check that no definition became redundant.
5. Regenerate the tables into a Zydis checkout with `datafiles/ path/to/zydis/` and commit the generated files there.

## Scripts

| Script                      | Arguments                             | Purpose                                                  |
| --------------------------- | ------------------------------------- | -------------------------------------------------------- |
| `gen_registers.py`          | `<registers.json> <zydis>` (both required) | Generates the register enum and lookup tables (`EnumRegister.h`, `EnumRegister.inc`, `RegisterLookup.inc`, `RegisterClassLookup.inc`) from `registers.json` into a Zydis checkout. |
| `generate_encoder_types.py` | `[zydis_path]` (defaults to `../../zydis`) | Parses a Zydis checkout's C sources to derive encoder-related type definitions used by the generator. |

## License

Zydis Generator is licensed under the MIT license.
