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
  System.Classes, Zydis.InstructionEditor;

type
  TZYCodeGenerator = class;

  TZYGeneratorTask = class abstract(TObject)
  strict protected
    class procedure WorkStart(Generator: TZYCodeGenerator; TotalWorkCount: Integer); static;
    class procedure WorkStep(Generator: TZYCodeGenerator); static;
    class procedure WorkEnd(Generator: TZYCodeGenerator); static;
  end;

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

  TZYGeneratorWorkStartEvent = procedure(Sender: TObject; ModuleId, TaskId: Integer;
    TotalWorkCount: Integer) of Object;
  TZYGeneratorWorkEvent      = procedure(Sender: TObject; WorkCount: Integer) of Object;
  TZYGeneratorWorkEndEvent   = TNotifyEvent;

  TZYCodeGenerator = class sealed(TObject)
  strict private
    FCurrentModuleId: Integer;
    FCurrentTaskId: Integer;
    FCurrentWorkCount: Integer;
  strict private
    FOnWorkStart: TZYGeneratorWorkStartEvent;
    FOnWork: TZYGeneratorWorkEvent;
    FOnWorkEnd: TZYGeneratorWorkEndEvent;
  strict private
    class function GetModuleDescription(ModuleId: Integer): String; inline; static;
    class function GetModuleCount: Integer; inline; static;
    class function GetTaskDescription(ModuleId, TaskId: Integer): String; inline; static;
    class function GetTaskCount(ModuleId: Integer): Integer; inline; static;
  private
    procedure WorkStart(TotalWorkCount: Integer); inline;
    procedure WorkStep; inline;
    procedure WorkEnd; inline;
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
    class property ModuleDescriptions[ModuleId: Integer]: String read GetModuleDescription;
    class property ModuleCount: Integer read GetModuleCount;
    class property TaskDescriptions[ModuleId, TaskId: Integer]: String read GetTaskDescription;
    class property TaskCount[ModuleId: Integer]: Integer read GetTaskCount;
  public
    property OnWorkStart: TZYGeneratorWorkStartEvent read FOnWorkStart write FOnWorkStart;
    property OnWork: TZYGeneratorWorkEvent read FOnWork write FOnWork;
    property OnWorkEnd: TZYGeneratorWorkEndEvent read FOnWorkEnd write FOnWorkEnd;
  end;

implementation

uses
  System.SysUtils, System.Generics.Collections, System.Generics.Defaults, Utils.Comparator,
  Utils.Container, Zydis.Enums, Zydis.Enums.Filters, Zydis.InstructionFilters,
  Zydis.Generator.Types, Zydis.Generator.Tables, Zydis.Generator.Enums, Zydis.Generator.Consts;

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

{$REGION 'Class: TZYGeneratorTask'}
class procedure TZYGeneratorTask.WorkEnd(Generator: TZYCodeGenerator);
begin
  Generator.WorkEnd;
end;

class procedure TZYGeneratorTask.WorkStart(Generator: TZYCodeGenerator; TotalWorkCount: Integer);
begin
  Generator.WorkStart(TotalWorkCount);
end;

class procedure TZYGeneratorTask.WorkStep(Generator: TZYCodeGenerator);
begin
  Generator.WorkStep;
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

  Definitions := nil;
  Snapshot    := nil;
  Operands    := nil;
  Encodings   := nil;
  Flags       := nil;
  FillChar(Enums, SizeOf(Enums), #0);

  E := TZYInstructionEditor.Create;
  try
    FCurrentModuleId := 0; // Preparing data tables
    FCurrentTaskId   := 0; // Selecting desired definitions
    DuplicateDefinitions(Editor, E, ISASets);
    FCurrentTaskId   := 1; // Creating definition list
    Definitions := TZYDefinitionList.Create(Self, E);
    FCurrentTaskId   := 2; // Creating instruction tree
    Snapshot := TZYTreeSnapshot.Create(Self, E, Definitions);
    FCurrentTaskId   := 3; // Creating operand list
    Operands := TZYUniqueOperandList.Create(Self, Definitions);
    FCurrentTaskId   := 4; // Gathering physical encodings
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
    FCurrentTaskId   := 5; // Gathering accessed flags
    // We don't need a custom equality comparator, because all of the zydis classes override
    // `TObject.Equals`
    Flags :=
      TZYUniqueDefinitionPropertyList<TZYInstructionFlagsInfo>.Create(Self, Definitions,
        function(D: TZYInstructionDefinition): TZYInstructionFlagsInfo
        begin
          Result := D.AffectedFlags;
        end, false);

    FCurrentModuleId := 1; // Preparing enums
    FCurrentTaskId   := 0; // Creating mnemonic enum
    Enums[0] := TZYGeneratorEnum.Create(Self, Definitions,
      function(D: TZYInstructionDefinition): String
      begin
        Result := D.Mnemonic;
      end, 'invalid');
    FCurrentTaskId   := 1; // Creating category enum
    Enums[1] := TZYGeneratorEnum.Create(Self, Definitions,
      function(D: TZYInstructionDefinition): String
      begin
        Result := D.Meta.Category;
      end, 'INVALID');
    FCurrentTaskId   := 2; // Creating isa-set enum
    Enums[2] := TZYGeneratorEnum.Create(Self, Definitions,
      function(D: TZYInstructionDefinition): String
      begin
        Result := D.Meta.Extension;
      end, 'INVALID');
    FCurrentTaskId   := 3; // Creating isa-extension enum
    Enums[3] := TZYGeneratorEnum.Create(Self, Definitions,
      function(D: TZYInstructionDefinition): String
      begin
        Result := D.Meta.ISASet;
      end, 'INVALID');

    FCurrentModuleId := 2; // Generating data tables
    FCurrentTaskId   := 0; // Generating definition list
    TZYDefinitionTableGenerator.Generate(
      Self, IncludeTrailingPathDelimiter(RootDirectory) +
      IncludeTrailingPathDelimiter(TZYGeneratorConsts.PathSource) +
      TZYGeneratorConsts.FilenameInstructions,
      Definitions, Operands, Enums[1], Enums[2], Enums[3], Flags);
    FCurrentTaskId   := 1; // Generating operand list
    TZYOperandTableGenerator.Generate(
      Self, IncludeTrailingPathDelimiter(RootDirectory) +
      IncludeTrailingPathDelimiter(TZYGeneratorConsts.PathSource) +
      TZYGeneratorConsts.FilenameOperands, Operands);
    FCurrentTaskId   := 2; // Generating physical encoding list
    TZYEncodingTableGenerator.Generate(
      Self, IncludeTrailingPathDelimiter(RootDirectory) +
      IncludeTrailingPathDelimiter(TZYGeneratorConsts.PathSource) +
      TZYGeneratorConsts.FilenameEncodings, Encodings);
    FCurrentTaskId   := 3; // Generating accessed flags list
    TZYAccessedFlagsTableGenerator.Generate(
      Self, IncludeTrailingPathDelimiter(RootDirectory) +
      IncludeTrailingPathDelimiter(TZYGeneratorConsts.PathSource) +
      TZYGeneratorConsts.FilenameAccessedFlags, Flags);
    FCurrentTaskId   := 4; // Generating decoder tables
    TZYDecoderTableGenerator.Generate(
      Self, IncludeTrailingPathDelimiter(RootDirectory) +
      IncludeTrailingPathDelimiter(TZYGeneratorConsts.PathSource) +
      TZYGeneratorConsts.FilenameDecoderTables, Snapshot, Encodings);

    FCurrentModuleId := 3; // Generating enums
    FCurrentTaskId   := 0; // Generating mnemonic enum
    TZYEnumGenerator.Generate(Self, RootDirectory, 'Mnemonic',
      'MNEMONIC_', Enums[0],
      [TZYEnumGeneratorFlag.GenerateNativeStrings, TZYEnumGeneratorFlag.ZydisString]);
    FCurrentTaskId   := 1; // Generating category enum
    TZYEnumGenerator.Generate(Self, RootDirectory, 'InstructionCategory',
      'CATEGORY_', Enums[1],
      [TZYEnumGeneratorFlag.GenerateNativeStrings]);
    FCurrentTaskId   := 2; // Generating isa-set enum
    TZYEnumGenerator.Generate(Self, RootDirectory, 'ISASet',
      'ISA_SET_' , Enums[2],
      [TZYEnumGeneratorFlag.GenerateNativeStrings]);
    FCurrentTaskId   := 3; // Generating isa-extension enum
    TZYEnumGenerator.Generate(Self, RootDirectory, 'ISAExt',
      'ISA_EXT_' , Enums[3],
      [TZYEnumGeneratorFlag.GenerateNativeStrings]);

    {Report.Clear;
    S := 0;
    for E := Low(TZYInstructionEncoding) to High(TZYInstructionEncoding) do
    begin
      Inc(S, Length(Definitions.UniqueItems[E]) *
        TZYGeneratorConsts.SIZEOF_INSTRUCTION_DEFINITION[E]);
    end;
    Report.CreateEntry('Basic."Instruction definitions"', Definitions.UniqueItemCount, S);
    Report.CreateEntry('Basic."Operand definitions"'    , Length(Operands.Items),
      Length(Operands.Items) * TZYGeneratorConsts.SIZEOF_OPERAND_DEFINITION);}
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

class function TZYCodeGenerator.GetModuleCount: Integer;
begin
  Result := 4;
end;

class function TZYCodeGenerator.GetModuleDescription(ModuleId: Integer): String;
begin
  case ModuleId of
    0: Result := 'Preparing data tables';
    1: Result := 'Preparing enums';
    2: Result := 'Generating data tables';
    3: Result := 'Generating enums'
      else raise EListError.CreateFmt('Index out of bounds (%d)', [ModuleId]);
  end;
end;

class function TZYCodeGenerator.GetTaskCount(ModuleId: Integer): Integer;
begin
  case ModuleId of
    0: Result := 6;
    1: Result := 4;
    2: Result := 6;
    3: Result := 4
      else raise EListError.CreateFmt('Index out of bounds (%d)', [ModuleId]);
  end;
end;

class function TZYCodeGenerator.GetTaskDescription(ModuleId, TaskId: Integer): String;
begin
  case ModuleId of
    0:
      case TaskId of
        0: Result := 'Selecting desired definitions';
        1: Result := 'Creating definition list';
        2: Result := 'Creating instruction tree';
        3: Result := 'Creating operand list';
        4: Result := 'Gathering physical encodings';
        5: Result := 'Gathering accessed flags'
          else raise EListError.CreateFmt('Index out of bounds (%d)', [TaskId]);
      end;
    1:
      case TaskId of
        0: Result := 'Creating mnemonic enum';
        1: Result := 'Creating category enum';
        2: Result := 'Creating isa-set enum';
        3: Result := 'Creating isa-extension enum'
          else raise EListError.CreateFmt('Index out of bounds (%d)', [TaskId]);
      end;
    2:
      case TaskId of
        0: Result := 'Generating definition list';
        1: Result := 'Generating operand list';
        2: Result := 'Generating physical encoding list';
        3: Result := 'Generating accessed flags list';
        4: Result := 'Generating decoder tables';
        5: Result := 'Generating encoder tables'
          else raise EListError.CreateFmt('Index out of bounds (%d)', [TaskId]);
      end;
    3:
      case TaskId of
        0: Result := 'Generating mnemonic enum';
        1: Result := 'Generating category enum';
        2: Result := 'Generating isa-set enum';
        3: Result := 'Generating isa-extension enum'
          else raise EListError.CreateFmt('Index out of bounds (%d)', [TaskId]);
      end else raise EListError.CreateFmt('Index out of bounds (%d)', [ModuleId]);
  end;
end;

procedure TZYCodeGenerator.WorkEnd;
begin
  if Assigned(FOnWorkEnd) then
  begin
    FOnWorkEnd(Self);
  end;
end;

procedure TZYCodeGenerator.WorkStart(TotalWorkCount: Integer);
begin
  FCurrentWorkCount := 0;
  if Assigned(FOnWorkStart) then
  begin
    FOnWorkStart(Self, FCurrentModuleId, FCurrentTaskId, TotalWorkCount);
  end;
end;

procedure TZYCodeGenerator.WorkStep;
begin
  Inc(FCurrentWorkCount);
  if Assigned(FOnWork) then
  begin
    FOnWork(Self, FCurrentWorkCount);
  end;
end;
{$ENDREGION}

end.
