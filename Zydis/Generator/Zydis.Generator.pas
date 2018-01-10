{***************************************************************************************************

  ZydisEditor

  Original Author : Florian Bernd

 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.

***************************************************************************************************}

unit Zydis.Generator;

interface

uses
  System.Generics.Collections, System.Classes, Zydis.Generator.Base, Zydis.Generator.Types,
  Zydis.InstructionEditor;

{$SCOPEDENUMS ON}

type
  PZYGeneratorReport = ^TZYGeneratorReport;
  TZYGeneratorReport = record
  public type
    TEntry = record
    public
      Name: String;
      Count: Integer;
      Size: Cardinal;
    end;
  strict private
    FEntries: TArray<TEntry>;
  strict private
    function GetEntryCount: Integer; inline;
    function GetEntry(Index: Integer): TEntry; inline;
  private
    procedure CreateEntry(const Name: String; Count: Integer; Size: Cardinal);
    procedure Clear; inline;
  public
    property EntryCount: Integer read GetEntryCount;
    property Entries[Index: Integer]: TEntry read GetEntry;
  end;

  TZYCodeGenerator = class sealed(TZYBaseGenerator)
  strict private type
    PZYGeneratorEnum = ^TZYGeneratorEnum;
  strict private
    class procedure GenerateReport(var Report: TZYGeneratorReport;
      const Definitions: TZYDefinitionList; const Snapshot: TZYTreeSnapshot;
      const Operands: TZYUniqueOperandList;
      const Encodings: TZYUniqueDefinitionPropertyList<TZYInstructionPartInfo>;
      const Flags: TZYUniqueDefinitionPropertyList<TZYInstructionFlagsInfo>;
      const Enums: array of TPair<String, PZYGeneratorEnum>); static;
  strict protected
    procedure InitGenerator(var ModuleInfo: TArray<TZYGeneratorModuleInfo>); override;
  public
    procedure GenerateCodeFiles(Editor: TZYInstructionEditor;
      const RootDirectory: String); overload; inline;
    procedure GenerateCodeFiles(Editor: TZYInstructionEditor;
      const RootDirectory: String; const ISASets: array of String); overload;
    procedure GenerateCodeFiles(Editor: TZYInstructionEditor;
      const RootDirectory: String; var Report: TZYGeneratorReport); overload;
    procedure GenerateCodeFiles(Editor: TZYInstructionEditor;
      const RootDirectory: String; const ISASets: array of String;
      var Report: TZYGeneratorReport); overload;
  public
    constructor Create;
  end;

implementation

uses
  System.SysUtils, System.Generics.Defaults, Utils.Comparator, Utils.Container,
  Zydis.Generator.Enums, Zydis.Enums, Zydis.Enums.Filters, Zydis.InstructionFilters,
  Zydis.Generator.Tables, Zydis.Generator.Consts;

{$REGION 'Class: TZYGeneratorReport'}
procedure TZYGeneratorReport.Clear;
begin
  SetLength(FEntries, 0);
end;

procedure TZYGeneratorReport.CreateEntry(const Name: String; Count: Integer; Size: Cardinal);
var
  E: TEntry;
begin
  E.Name := Name;
  E.Count := Count;
  E.Size := Size;
  TArrayHelper.Add<TEntry>(FEntries, E);
end;

function TZYGeneratorReport.GetEntry(Index: Integer): TEntry;
begin
  Result := FEntries[Index];
end;

function TZYGeneratorReport.GetEntryCount: Integer;
begin
  Result := Length(FEntries);
end;
{$ENDREGION}

{$REGION 'Class: TZYCodeGenerator'}
procedure TZYCodeGenerator.GenerateCodeFiles(Editor: TZYInstructionEditor;
  const RootDirectory: String);
var
  Report: TZYGeneratorReport;
begin
  GenerateCodeFiles(Editor, RootDirectory, [], Report);
end;

procedure TZYCodeGenerator.GenerateCodeFiles(Editor: TZYInstructionEditor;
  const RootDirectory: String; const ISASets: array of String);
var
  Report: TZYGeneratorReport;
begin
  GenerateCodeFiles(Editor, RootDirectory, ISASets, Report);
end;

procedure TZYCodeGenerator.GenerateCodeFiles(Editor: TZYInstructionEditor;
  const RootDirectory: String; var Report: TZYGeneratorReport);
begin
  GenerateCodeFiles(Editor, RootDirectory, [], Report);
end;

constructor TZYCodeGenerator.Create;
begin
  inherited Create;
end;

procedure TZYCodeGenerator.GenerateCodeFiles(Editor: TZYInstructionEditor;
  const RootDirectory: String; const ISASets: array of String; var Report: TZYGeneratorReport);

function DefinitionInISASets(Definition: TZYInstructionDefinition;
  const ISASets: array of String): Boolean;
var
  I: Integer;
begin
  Result := false;
  for I := Low(ISASets) to High(ISASets) do
  begin
    if (AnsiSameText(ISASets[I], Definition.Meta.ISASet)) then
    begin
      Result := true;
      Exit;
    end;
  end;
end;

function DuplicateDefinition(Source, Target: TZYInstructionEditor;
  Index: Integer): TZYInstructionDefinition; inline;
begin
  Result := Target.CreateDefinition(Source.Definitions[Index].Mnemonic);
  Result.BeginUpdate;
  try
    Result.Assign(Source.Definitions[Index]);
    Result.Insert;
  finally
    Result.EndUpdate;
  end;
end;

procedure DuplicateDefinitions(Source, Target: TZYInstructionEditor;
  const ISASets: array of String);
var
  D: TZYInstructionDefinition;
  T: array[TZYInstructionEncoding] of Boolean;
  E: TZYInstructionEncoding;
  I: Integer;
begin
  WorkStart(Editor.DefinitionCount);
  Target.BeginUpdate;
  try
    Target.Reset;
    FillChar(T, SizeOf(T), #0);
    for I := 0 to Source.DefinitionCount - 1 do
    begin
      if (Length(ISASets) = 0) or (DefinitionInISASets(Source.Definitions[I], ISASets)) then
      begin
        D := DuplicateDefinition(Source, Target, I);
        T[D.Encoding] := True;
      end;
      WorkStep;
    end;
    // Make sure we have at least one instruction-definition for every encoding. Generation
    // will fail otherwise.
    for E := Low(TZYInstructionEncoding) to High(TZYInstructionEncoding) do
    begin
      if (not T[E]) then
      begin
        D := Target.CreateDefinition('invalid');
        D.BeginUpdate;
        try
          D.Encoding := E;
          D.Insert;
        finally
          D.EndUpdate;
        end;
      end;
    end;
  finally
    Target.EndUpdate;
  end;
  WorkEnd;
end;

type
  TZYEnumOption = (Native, Bindings);
  TZYEnumOptionScope = set of TZYEnumOption;

function MakeEnumGeneratorTaskList(const AEnumName, AItemPrefix: String;
  FlagsNative, FlagsBindings: TZYEnumGeneratorFlags): TZYEnumGeneratorTaskItems;
begin
  Result := [
    TZYEnumGeneratorTaskItem.Create(
      TZYEnumGeneratorC,
      TZYGeneratorConsts.PathIncludeC,
      TZYGeneratorConsts.PathSourceC,
      AEnumName,
      AItemPrefix,
      FlagsNative
    ),
    TZYEnumGeneratorTaskItem.Create(
      TZYEnumGeneratorPascal,
      TZYGeneratorConsts.PathIncludePascal,
      TZYGeneratorConsts.PathSourcePascal,
      AEnumName,
      AItemPrefix,
      FlagsBindings
    )
  ];
end;

var
  E: TZYInstructionEditor;
  I: Integer;
  Definitions: TZYDefinitionList;
  Snapshot   : TZYTreeSnapshot;
  Operands   : TZYUniqueOperandList;
  Encodings  : TZYUniqueDefinitionPropertyList<TZYInstructionPartInfo>;
  Flags      : TZYUniqueDefinitionPropertyList<TZYInstructionFlagsInfo>;
  Enums      : array[0..3] of TZYGeneratorEnum;
begin
  if (Editor.RootNode.HasConflicts) then
  begin
    raise Exception.Create('Database has unresolved conflicts.');
  end;

  Reset;
  Definitions := nil;
  Snapshot    := nil;
  Operands    := nil;
  Encodings   := nil;
  Flags       := nil;
  FillChar(Enums, SizeOf(Enums), #0);

  E := TZYInstructionEditor.Create;
  try
    // Selecting desired definitions
    DuplicateDefinitions(Editor, E, ISASets);
    // Creating definition list
    Definitions := TZYDefinitionList.Create(Self, E);
    // Creating instruction tree
    Snapshot := TZYTreeSnapshot.Create(Self, E, Definitions);
    // Creating operand list
    Operands := TZYUniqueOperandList.Create(Self, Definitions);
    // Gathering physical encodings
    // We don't need a custom equality comparator, because all of the zydis classes override
    // `TObject.Equals`, but we have to implement a custom comparator in order to support objects
    // with absolute order.
    Encodings :=
      TZYUniqueDefinitionPropertyList<TZYInstructionPartInfo>.Create(Self, Definitions,
        function(D: TZYInstructionDefinition): TZYInstructionPartInfo
        begin
          Result := D.InstructionParts;
        end, true,
        TEqualityComparer<TZYInstructionPartInfo>.Default,
        TComparer<TZYInstructionPartInfo>.Construct(
          function(const Left, Right: TZYInstructionPartInfo): Integer
          begin
            Result := TComparator.Init
              .Comparing<TZYInstructionParts>(Left.Parts, Right.Parts,
                function(const Left, Right: TZYInstructionParts): Integer
                var
                  P: TZYInstructionPart;
                begin
                  Result := 0;
                  for P := Low(TZYInstructionPart) to High(TZYInstructionPart) do
                  begin
                    if (P in Left ) then Inc(Result, Ord(P) + 1);
                    if (P in Right) then Dec(Result, Ord(P) + 1);
                  end;
                end)
              .Comparing(Left.Displacement.Width16, Right.Displacement.Width16)
              .Comparing(Left.Displacement.Width32, Right.Displacement.Width32)
              .Comparing(Left.Displacement.Width64, Right.Displacement.Width64)
              .Comparing(Left.ImmediateA.IsSigned, Right.ImmediateA.IsSigned)
              .Comparing(Left.ImmediateA.IsRelative, Right.ImmediateA.IsRelative)
              .Comparing(Left.ImmediateA.Width16, Right.ImmediateA.Width16)
              .Comparing(Left.ImmediateA.Width32, Right.ImmediateA.Width32)
              .Comparing(Left.ImmediateA.Width64, Right.ImmediateA.Width64)
              .Comparing(Left.ImmediateB.IsSigned, Right.ImmediateB.IsSigned)
              .Comparing(Left.ImmediateB.IsRelative, Right.ImmediateB.IsRelative)
              .Comparing(Left.ImmediateB.Width16, Right.ImmediateB.Width16)
              .Comparing(Left.ImmediateB.Width32, Right.ImmediateB.Width32)
              .Comparing(Left.ImmediateB.Width64, Right.ImmediateB.Width64)
              .Compare;
          end));
    // Gathering accessed flags
    // We don't need a custom equality comparator, because all of the zydis classes override
    // `TObject.Equals`
    Flags :=
      TZYUniqueDefinitionPropertyList<TZYInstructionFlagsInfo>.Create(Self, Definitions,
        function(D: TZYInstructionDefinition): TZYInstructionFlagsInfo
        begin
          Result := D.AffectedFlags;
        end, false);

    // Creating mnemonic enum
    Enums[0] := TZYGeneratorEnum.Create(Self, Definitions,
      function(D: TZYInstructionDefinition): String
      begin
        Result := D.Mnemonic;
      end, 'invalid');
    // Creating category enum
    Enums[1] := TZYGeneratorEnum.Create(Self, Definitions,
      function(D: TZYInstructionDefinition): String
      begin
        Result := D.Meta.Category;
      end, 'INVALID');
    // Creating isa-set enum
    Enums[2] := TZYGeneratorEnum.Create(Self, Definitions,
      function(D: TZYInstructionDefinition): String
      begin
        Result := D.Meta.Extension;
      end, 'INVALID');
    // Creating isa-extension enum
    Enums[3] := TZYGeneratorEnum.Create(Self, Definitions,
      function(D: TZYInstructionDefinition): String
      begin
        Result := D.Meta.ISASet;
      end, 'INVALID');

    // Generating definition list
    TZYDefinitionTableGenerator.Generate(
      Self, IncludeTrailingPathDelimiter(RootDirectory) +
      IncludeTrailingPathDelimiter(TZYGeneratorConsts.PathSource) +
      TZYGeneratorConsts.FilenameInstructions,
      Definitions, Operands, Enums[1], Enums[2], Enums[3], Flags);
    // Generating operand list
    TZYOperandTableGenerator.Generate(
      Self, IncludeTrailingPathDelimiter(RootDirectory) +
      IncludeTrailingPathDelimiter(TZYGeneratorConsts.PathSource) +
      TZYGeneratorConsts.FilenameOperands, Operands);
    // Generating physical encoding list
    TZYEncodingTableGenerator.Generate(
      Self, IncludeTrailingPathDelimiter(RootDirectory) +
      IncludeTrailingPathDelimiter(TZYGeneratorConsts.PathSource) +
      TZYGeneratorConsts.FilenameEncodings, Encodings);
    // Generating accessed flags list
    TZYAccessedFlagsTableGenerator.Generate(
      Self, IncludeTrailingPathDelimiter(RootDirectory) +
      IncludeTrailingPathDelimiter(TZYGeneratorConsts.PathSource) +
      TZYGeneratorConsts.FilenameAccessedFlags, Flags);
    // Generating decoder tables
    TZYDecoderTableGenerator.Generate(
      Self, IncludeTrailingPathDelimiter(RootDirectory) +
      IncludeTrailingPathDelimiter(TZYGeneratorConsts.PathSource) +
      TZYGeneratorConsts.FilenameDecoderTables, Snapshot, Encodings);
    // Generating encoder tables
    SkipTask;

    // Generating mnemonic enum
    TZYEnumGeneratorTask.Generate(Self, RootDirectory, MakeEnumGeneratorTaskList(
      'Mnemonic',
      'MNEMONIC_',
      [TZYEnumGeneratorFlag.GenerateStrings, TZYEnumGeneratorFlag.UseCustomStringType],
      []),
      Enums[0].Items);
    // Generating category enum
    TZYEnumGeneratorTask.Generate(Self, RootDirectory, MakeEnumGeneratorTaskList(
      'InstructionCategory',
      'CATEGORY_',
      [TZYEnumGeneratorFlag.GenerateStrings],
      []),
      Enums[1].Items);
    // Generating isa-set enum
    TZYEnumGeneratorTask.Generate(Self, RootDirectory, MakeEnumGeneratorTaskList(
      'ISASet',
      'ISA_SET_',
      [TZYEnumGeneratorFlag.GenerateStrings],
      []),
      Enums[2].Items);
    // Generating isa-extension enum
    TZYEnumGeneratorTask.Generate(Self, RootDirectory, MakeEnumGeneratorTaskList(
      'ISAExt',
      'ISA_EXT_',
      [TZYEnumGeneratorFlag.GenerateStrings],
      []),
      Enums[3].Items);

    // Create report
    Report.Clear;
    GenerateReport(Report, Definitions, Snapshot, Operands, Encodings, Flags,
      [TPair<String, PZYGeneratorEnum>.Create('Mnemonic'           , @Enums[0]),
       TPair<String, PZYGeneratorEnum>.Create('InstructionCategory', @Enums[1]),
       TPair<String, PZYGeneratorEnum>.Create('ISASet'             , @Enums[2]),
       TPair<String, PZYGeneratorEnum>.Create('ISAExt'             , @Enums[3])]);
  finally
    Definitions.Free;
    Snapshot.Free;
    Operands.Free;
    Encodings.Free;
    Flags.Free;
    for I := Low(Enums) to High(Enums) do
    begin
      Enums[I].Free;
    end;
    E.Free;
  end;
end;

class procedure TZYCodeGenerator.GenerateReport(var Report: TZYGeneratorReport;
  const Definitions: TZYDefinitionList; const Snapshot: TZYTreeSnapshot;
  const Operands: TZYUniqueOperandList;
  const Encodings: TZYUniqueDefinitionPropertyList<TZYInstructionPartInfo>;
  const Flags: TZYUniqueDefinitionPropertyList<TZYInstructionFlagsInfo>;
  const Enums: array of TPair<String, PZYGeneratorEnum>);
const
  TABLE_NAMES: array[TZYInstructionFilterClass] of String = (
    '',
    'XOP',
    'VEX',
    'EMVEX',
    'Opcode',
    'Mode',
    'ModeCompact',
    'ModrmMod',
    'ModrmModCompact',
    'ModrmReg',
    'ModrmRm',
    'MandatoryPrefix',
    'OperandSize',
    'AddressSize',
    'VectorLength',
    'REXW',
    'REXB',
    'EVEXB',
    'MVEXE',
    'ModeAMD',
    'ModeKNC',
    'ModeMPX',
    'ModeCET',
    'ModeLZCNT',
    'ModeTZCNT'
  );
var
  S, T, Y, Z: Cardinal;
  I, J, K: Integer;
  E: TZYInstructionEncoding;
  F: TZYInstructionFilterClass;
begin
  S := 0;
  for E := Low(TZYInstructionEncoding) to High(TZYInstructionEncoding) do
  begin
    Inc(S, Length(Definitions.UniqueItems[E]) * TZYGeneratorConsts.SizeOfInstructionDefinition[E]);
  end;
  T := S;
  Y := 0;
  for F := Low(TZYInstructionFilterClass) to High(TZYInstructionFilterClass) do
  begin
    if (F = ifcInvalid) then
    begin
      Continue;
    end;
    I := TZYInstructionFilterInfo.Info[F].NumberOfValues;
    Inc(Y, Length(Snapshot.Filters[F]) * I * TZYGeneratorConsts.SizeOfDecoderTreeNode);
    Inc(T, Length(Snapshot.Filters[F]) * I * TZYGeneratorConsts.SizeOfDecoderTreeNode);
  end;
  Inc(T, Length(Operands.Items) * TZYGeneratorConsts.SizeOfOperandDefinition);
  Inc(T, Length(Encodings.Items) * TZYGeneratorConsts.SizeOfInstructionEncoding);
  Inc(T, Length(Flags.Items) * TZYGeneratorConsts.SizeOfAccessedFlags);
  K := 0;
  Z := 0;
  for I := Low(Enums) to High(Enums) do
  begin
    Inc(K, Length(Enums[I].Value^.Items));
    for J := Low(Enums[I].Value^.Items) to High(Enums[I].Value^.Items) do
    begin
      Inc(Z, Length(Enums[I].Value^.Items[J]));
      Inc(T, Length(Enums[I].Value^.Items[J]));
    end;
  end;

  Report.CreateEntry('Overview.Total Size', 0, T);
  Report.CreateEntry('Overview.Instruction Definitions', Definitions.UniqueItemCount, S);
  Report.CreateEntry('Overview.Filters', Snapshot.FilterCount, Y);
  Report.CreateEntry('Overview.Operand Definitions', Length(Operands.Items),
    Length(Operands.Items) * TZYGeneratorConsts.SizeOfOperandDefinition);
  Report.CreateEntry('Overview.Instruction Encodings', Length(Encodings.Items),
    Length(Encodings.Items) * TZYGeneratorConsts.SizeOfInstructionEncoding);
  Report.CreateEntry('Overview.Accessed Flags', Length(Flags.Items),
    Length(Flags.Items) * TZYGeneratorConsts.SizeOfAccessedFlags);
  Report.CreateEntry('Overview.Enums', K, Z);

  for E := Low(TZYInstructionEncoding) to High(TZYInstructionEncoding) do
  begin
    Report.CreateEntry('Instruction Definitions.' + TZYEnumInstructionEncoding.ZydisStrings[E],
      Length(Definitions.UniqueItems[E]),
      Length(Definitions.UniqueItems[E]) * TZYGeneratorConsts.SizeOfInstructionDefinition[E]);
  end;

  for F := Low(TZYInstructionFilterClass) to High(TZYInstructionFilterClass) do
  begin
    if (F = ifcInvalid) then
    begin
      Continue;
    end;
    Report.CreateEntry('Filters.' + TABLE_NAMES[F], Length(Snapshot.Filters[F]),
      Length(Snapshot.Filters[F]) * TZYInstructionFilterInfo.Info[F].NumberOfValues *
      TZYGeneratorConsts.SizeOfDecoderTreeNode);
  end;

  for I := Low(Enums) to High(Enums) do
  begin
    S := 0;
    for J := Low(Enums[I].Value^.Items) to High(Enums[I].Value^.Items) do
    begin
      Inc(S, Length(Enums[I].Value^.Items[J]));
    end;
    Report.CreateEntry('Enums.' + Enums[I].Key, Length(Enums[I].Value^.Items), S);
  end;
end;

procedure TZYCodeGenerator.InitGenerator(var ModuleInfo: TArray<TZYGeneratorModuleInfo>);

procedure RegisterModule(const Description: String; const TaskDescriptions: array of String);
var
  I: Integer;
begin
  SetLength(ModuleInfo, Length(ModuleInfo) + 1);
  SetLength(ModuleInfo[High(ModuleInfo)].Tasks, Length(TaskDescriptions));
  for I := Low(TaskDescriptions) to High(TaskDescriptions) do
  begin
    ModuleInfo[High(ModuleInfo)].Tasks[I].Description := TaskDescriptions[I];
  end;
end;

begin
  RegisterModule('Preparing data tables',
  [
    'Selecting desired definitions',
    'Creating definition list',
    'Creating instruction tree',
    'Creating operand list',
    'Gathering physical encodings',
    'Gathering accessed flags'
  ]);
  RegisterModule('Preparing enums',
  [
    'Creating mnemonic enum',
    'Creating category enum',
    'Creating isa-set enum',
    'Creating isa-extension enum'
  ]);
  RegisterModule('Generating data tables',
  [
    'Generating definition list',
    'Generating operand list',
    'Generating physical encoding list',
    'Generating accessed flags list',
    'Generating decoder tables',
    'Generating encoder tables'
  ]);
  RegisterModule('Generating enums',
  [
    'Generating mnemonic enum',
    'Generating category enum',
    'Generating isa-set enum',
    'Generating isa-extension enum'
  ]);
end;
{$ENDREGION}

end.
