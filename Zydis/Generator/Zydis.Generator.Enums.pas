{***************************************************************************************************

  Zydis Code Generator

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

unit Zydis.Generator.Enums;

interface

uses
  Zydis.Generator.Base, System.SysUtils;

{$SCOPEDENUMS ON}

type
  TZYEnumGeneratorFlag = (
    {**
     * @brief Generates this enum in a private namespace.
     * }
    IsPrivateEnum,
    {**
     * @brief Generates a string array for the items of the enum.
     * }
    GenerateStrings,
    {**
     * @brief Uses the custom `ZydisStaticString` datatype instead of the default string type.
     * }
    UseCustomStringType
  );
  TZYEnumGeneratorFlags = set of TZYEnumGeneratorFlag;

  TZYEnumGenerator = class;
  TZYEnumGeneratorClass = class of TZYEnumGenerator;

  TZYEnumGeneratorTaskItem = record
  public
    Generator: TZYEnumGeneratorClass;
    PathInclude: String;
    PathSource: String;
    EnumName: String;
    ItemPrefix: String;
    Flags: TZYEnumGeneratorFlags;
  public
    constructor Create(AGenerator: TZYEnumGeneratorClass; const APathInclude, APathSource: String;
      const AEnumName, AItemPrefix: String; AFlags: TZYEnumGeneratorFlags);
  end;
  TZYEnumGeneratorTaskItems = TArray<TZYEnumGeneratorTaskItem>;

  TZYEnumGeneratorTask = class sealed(TZYGeneratorTask)
  private
    class procedure WorkStep(AGenerator: TZYBaseGenerator); static; inline;
  strict protected
    constructor Create;
  public
    class procedure Generate(AGenerator: TZYBaseGenerator; const ARootDirectory: String;
      const ATasks: TZYEnumGeneratorTaskItems; const AItems: array of String); static;
  end;

  TZYEnumGenerator = class abstract(TObject)
  strict protected
    constructor Create;
  strict protected
    class procedure WorkStep(AGenerator: TZYBaseGenerator); static; inline;
  protected
    class function GetName: String; virtual; abstract;
    class function GetMaxWorkCount(const ATask: TZYEnumGeneratorTaskItem;
      const AItems: array of String): Integer; virtual;
    class procedure Generate(AGenerator: TZYBaseGenerator; const ARootDirectory: String;
      const ATask: TZYEnumGeneratorTaskItem; const AItems: array of String); virtual; abstract;
  end;

  TZYEnumGeneratorC = class sealed(TZYEnumGenerator)
  strict private
    class function IsKeyword(const S: String): Boolean; static;
  protected
    class function GetName: String; override;
    class procedure Generate(AGenerator: TZYBaseGenerator; const ARootDirectory: String;
      const ATask: TZYEnumGeneratorTaskItem; const AItems: array of String); override;
  end;

  TZYEnumGeneratorCPP = class sealed(TZYEnumGenerator)
  strict private
    class function IsKeyword(const S: String): Boolean; static;
  protected
    class function GetName: String; override;
    class procedure Generate(AGenerator: TZYBaseGenerator; const ARootDirectory: String;
      const ATask: TZYEnumGeneratorTaskItem; const AItems: array of String); override;
  end;

  TZYEnumGeneratorJava = class sealed(TZYEnumGenerator)
  strict private
    class function IsKeyword(const S: String): Boolean; static;
  protected
    class function GetName: String; override;
    class procedure Generate(AGenerator: TZYBaseGenerator; const ARootDirectory: String;
      const ATask: TZYEnumGeneratorTaskItem; const AItems: array of String); override;
  end;

  TZYEnumGeneratorPascal = class sealed(TZYEnumGenerator)
  strict private
    class function IsKeyword(const S: String): Boolean; static;
  protected
    class function GetName: String; override;
    class procedure Generate(AGenerator: TZYBaseGenerator; const ARootDirectory: String;
      const ATask: TZYEnumGeneratorTaskItem; const AItems: array of String); override;
  end;

  TZYEnumGeneratorPython = class sealed(TZYEnumGenerator)
  strict private
    class function IsKeyword(const S: String): Boolean; static;
  protected
    class function GetName: String; override;
    class procedure Generate(AGenerator: TZYBaseGenerator; const ARootDirectory: String;
      const ATask: TZYEnumGeneratorTaskItem; const AItems: array of String); override;
  end;

  TZYEnumGeneratorRust = class sealed(TZYEnumGenerator)
  strict private
    class function IsKeyword(const S: String): Boolean; static;
  protected
    class function GetName: String; override;
    class procedure Generate(AGenerator: TZYBaseGenerator; const ARootDirectory: String;
      const ATask: TZYEnumGeneratorTaskItem; const AItems: array of String); override;
  end;

implementation

uses
  System.Classes, System.StrUtils, System.Math;

{$REGION 'Class: TZYEnumGeneratorTaskItem'}
constructor TZYEnumGeneratorTaskItem.Create(AGenerator: TZYEnumGeneratorClass; const APathInclude,
  APathSource: String; const AEnumName, AItemPrefix: String; AFlags: TZYEnumGeneratorFlags);
begin
  Generator := AGenerator;
  PathInclude := APathInclude;
  PathSource := APathSource;
  EnumName := AEnumName;
  ItemPrefix := AItemPrefix;
  Flags := AFlags;
end;
{$ENDREGION}

{$REGION 'Class: TZYEnumGeneratorTask'}
constructor TZYEnumGeneratorTask.Create;
begin
  inherited Create;
end;

class procedure TZYEnumGeneratorTask.Generate(AGenerator: TZYBaseGenerator;
  const ARootDirectory: String; const ATasks: TZYEnumGeneratorTaskItems;
  const AItems: array of String);
var
  I, N: Integer;
begin
  Assert(Length(ATasks) > 0);
  Assert(Length(AItems) > 0);
  N := 0;
  for I := Low(ATasks) to High(ATasks) do
  begin
    Inc(N, ATasks[I].Generator.GetMaxWorkCount(ATasks[I], AItems));
  end;
  WorkStart(AGenerator, N);
  for I := Low(ATasks) to High(ATasks) do
  begin
    ATasks[I].Generator.Generate(AGenerator, ARootDirectory, ATasks[I], AItems);
  end;
  WorkEnd(AGenerator);
end;

class procedure TZYEnumGeneratorTask.WorkStep(AGenerator: TZYBaseGenerator);
begin
  TZYGeneratorTask.WorkStep(AGenerator);
end;
{$ENDREGION}

{$REGION 'Class: TZYEnumGenerator'}
constructor TZYEnumGenerator.Create;
begin
  inherited Create;
end;

class function TZYEnumGenerator.GetMaxWorkCount(const ATask: TZYEnumGeneratorTaskItem;
  const AItems: array of String): Integer;
begin
  Result := Length(AItems);
  if (TZYEnumGeneratorFlag.GenerateStrings in ATask.Flags) then
  begin
    Result := Result * 2;
  end;
end;

class procedure TZYEnumGenerator.WorkStep(AGenerator: TZYBaseGenerator);
begin
  TZYEnumGeneratorTask.WorkStep(AGenerator);
end;
{$ENDREGION}

{$REGION 'Class: TZYEnumGeneratorC'}
class procedure TZYEnumGeneratorC.Generate(AGenerator: TZYBaseGenerator;
  const ARootDirectory: String; const ATask: TZYEnumGeneratorTaskItem;
  const AItems: array of String);
var
  F, N, S, T, U: String;
  W: TStreamWriter;
  I, X, Y: Integer;
begin
  F := IncludeTrailingPathDelimiter(ARootDirectory);
  if (TZYEnumGeneratorFlag.IsPrivateEnum in ATask.Flags) then
  begin
    F := F + IncludeTrailingPathDelimiter(ATask.PathSource) + 'Enum' + ATask.EnumName + '.h';
  end else
  begin
    F := F + IncludeTrailingPathDelimiter(ATask.PathInclude) + 'Enum' + ATask.EnumName + '.h';
  end;
  N := 'Zydis' + ATask.EnumName;
  if (N.EndsWith('y')) then
  begin
    Delete(N, Length(N), 1);
    N := N + 'ies';
  end else
  begin
    N := N + 's';
  end;
  X := 0;
  for I := Low(AItems) to High(AItems)  do
  begin
    if (not AItems[I].StartsWith('//')) then Inc(X);
  end;
  X := Floor(Log2(X - 1)) + 1;
  Y := 8;
  if (X > 64) then Assert(false) else
  if (X > 32) then Y := 64 else
  if (X > 16) then Y := 32 else
  if (X >  8) then Y := 16;
  W := TStreamWriter.Create(F);
  try
    W.AutoFlush := true;
    W.NewLine := sLineBreak;
    W.Write('/**%s * @brief   Defines the `Zydis%s` datatype.%s */%s',
      [sLineBreak, ATask.EnumName, sLineBreak, sLineBreak]);
    W.Write('typedef ZydisU%d Zydis%s;', [Y, ATask.EnumName]);
    W.WriteLine;
    W.WriteLine;
    W.Write('/**%s * @brief   Values that represent `Zydis%s` elements.%s */%s',
      [sLineBreak, ATask.EnumName, sLineBreak, sLineBreak]);
    W.Write('enum %s', [N]);
    W.WriteLine;
    W.Write('{');
    W.WriteLine;
    for I := Low(AItems) to High(AItems) do
    begin
      if (AItems[I].StartsWith('//')) then
      begin
        if (AItems[I] <> '//') then
        begin
          W.Write('    %s', [AItems[I]]);
        end;
      end else
      begin
        T := AItems[I].ToUpper;
        W.Write('    %s%s%s,', ['ZYDIS_', ATask.ItemPrefix, T]);
      end;
      W.WriteLine;
      WorkStep(AGenerator);
    end;
    Assert(not T.StartsWith('//'));
    W.WriteLine;
    W.Write('    /**%s     * @brief   Maximum value of this enum.%s     */%s',
      [sLineBreak, sLineBreak, sLineBreak]);
    W.Write('    %s%sMAX_VALUE = %s%s%s,%s',
      ['ZYDIS_', ATask.ItemPrefix, 'ZYDIS_', ATask.ItemPrefix, T, sLineBreak]);
    W.Write('    /**%s     * @brief   Minimum amount of bits required to store a value of this ' +
      'enum.%s     */%s', [sLineBreak, sLineBreak, sLineBreak]);
    W.Write('    %s%sMIN_BITS  = 0x%.4X',
      ['ZYDIS_', ATask.ItemPrefix, X]);
    W.WriteLine;
    W.Write('};');
    W.WriteLine;
  finally
    W.Free;
  end;
  if (not (TZYEnumGeneratorFlag.GenerateStrings in ATask.Flags)) then
  begin
    Exit;
  end;
  N := 'zydis' + ATask.EnumName + 'Strings';
  F := IncludeTrailingPathDelimiter(ARootDirectory) +
    IncludeTrailingPathDelimiter(ATask.PathSource) + 'Enum' + ATask.EnumName + '.inc';
  S := ',';
  U := 'char*';
  if (TZYEnumGeneratorFlag.UseCustomStringType in ATask.Flags) then
  begin
    U := 'ZydisStaticString';
  end;
  W := TStreamWriter.Create(F);
  try
    W.AutoFlush := true;
    W.NewLine := sLineBreak;
    W.Write('static const %s %s[] =', [U, N]);
    W.WriteLine;
    W.Write('{');
    W.WriteLine;
    for I := Low(AItems) to High(AItems)  do
    begin
      if (I = High(AItems)) then
      begin
        S := '';
      end;
      if (AItems[I].StartsWith('//')) then
      begin
        if (AItems[I] <> '//') then
        begin
          W.Write('    %s', [AItems[I]]);
        end;
      end else
      begin
        if (TZYEnumGeneratorFlag.UseCustomStringType in ATask.Flags) then
        begin
          W.Write('    ZYDIS_MAKE_STATIC_STRING("%s")%s', [AItems[I], S]);
        end else
        begin
          W.Write('    "%s"%s', [AItems[I], S]);
        end;
      end;
      W.WriteLine;
      WorkStep(AGenerator);
    end;
    Assert(not T.StartsWith('//'));
    W.Write('};');
    W.WriteLine;
  finally
    W.Free;
  end;
end;

class function TZYEnumGeneratorC.GetName: String;
begin
  Result := 'C';
end;

class function TZYEnumGeneratorC.IsKeyword(const S: String): Boolean;
const
  KEYWORDS: array of String = [
    'auto',
    'break',
    'case',
    'char',
    'const',
    'continue',
    'default',
    'do',
    'double',
    'else',
    'enum',
    'extern',
    'flat',
    'for',
    'goto',
    'if',
    'int',
    'long',
    'register',
    'return',
    'short',
    'signed',
    'sizeof',
    'static',
    'struct',
    'switch',
    'typedef',
    'union',
    'unsigned',
    'void',
    'volatile',
    'while'
  ];
begin
  Result := System.StrUtils.AnsiIndexStr(S, KEYWORDS) >= 0;
end;
{$ENDREGION}

{$REGION 'Class: TZYEnumGeneratorCPP'}
class procedure TZYEnumGeneratorCPP.Generate(AGenerator: TZYBaseGenerator;
  const ARootDirectory: String; const ATask: TZYEnumGeneratorTaskItem;
  const AItems: array of String);
begin
  // TODO:
end;

class function TZYEnumGeneratorCPP.GetName: String;
begin
  Result := 'C++';
end;

class function TZYEnumGeneratorCPP.IsKeyword(const S: String): Boolean;
begin
  Result := false;
  // TODO:
end;
{$ENDREGION}

{$REGION 'Class: TZYEnumGeneratorJava'}
class procedure TZYEnumGeneratorJava.Generate(AGenerator: TZYBaseGenerator;
  const ARootDirectory: String; const ATask: TZYEnumGeneratorTaskItem;
  const AItems: array of String);
begin
  // TODO:
end;

class function TZYEnumGeneratorJava.GetName: String;
begin
  Result := 'Java';
end;

class function TZYEnumGeneratorJava.IsKeyword(const S: String): Boolean;
begin
  Result := false;
  // TODO:
end;
{$ENDREGION}

{$REGION 'Class: TZYEnumGeneratorPascal'}
class procedure TZYEnumGeneratorPascal.Generate(AGenerator: TZYBaseGenerator;
  const ARootDirectory: String; const ATask: TZYEnumGeneratorTaskItem;
  const AItems: array of String);
var
  F, N, S: String;
  X, Y: Integer;
  W: TStreamWriter;
  I: Integer;
begin
  F := IncludeTrailingPathDelimiter(ARootDirectory) +
    IncludeTrailingPathDelimiter(ATask.PathInclude) + 'Zydis.Enum.' + ATask.EnumName + '.inc';
  N := 'TZydis' + ATask.EnumName;
  S := ',';
  X := 0;
  for I := Low(AItems) to High(AItems)  do
  begin
    if (not AItems[I].StartsWith('//')) then Inc(X);
  end;
  X := Floor(Log2(X - 1)) + 1;
  Y := 1;
  if (X > 64) then Assert(false) else
  if (X > 32) then Y := 8 else
  if (X > 16) then Y := 4 else
  if (X >  8) then Y := 2;
  W := TStreamWriter.Create(F);
  try
    W.AutoFlush := true;
    W.NewLine := sLineBreak;
    W.Write('type');
    W.WriteLine;
    W.Write('  {$Z%d}%s', [Y, sLineBreak]);
    W.Write('  %s = (', [N]);
    W.WriteLine;
    for I := Low(AItems) to High(AItems)  do
    begin
      if (I = High(AItems)) then
      begin
        S := '';
      end;
      if (AItems[I].StartsWith('//')) then
      begin
        if (AItems[I] = '//') then
        begin
          W.WriteLine;
        end else
        begin
          W.Write('    %s%s', [AItems[I], sLineBreak]);
        end;
      end else
      begin
        W.Write('    ZYDIS_%s%s,%s', [ATask.ItemPrefix, AItems[I].ToUpper, sLineBreak]);
      end;
      WorkStep(AGenerator);
    end;
    W.WriteLine;
    W.Write('    ZYDIS_%sMAX_VALUE = ZYDIS_%s%s%s', [ATask.ItemPrefix, ATask.ItemPrefix,
      AItems[High(AItems)].ToUpper, sLineBreak]);
    W.Write('  );');
    W.WriteLine;
  finally
    W.Free;
  end;
  if (not (TZYEnumGeneratorFlag.GenerateStrings in ATask.Flags)) then
  begin
    Exit;
  end;
  F := IncludeTrailingPathDelimiter(ARootDirectory) +
    IncludeTrailingPathDelimiter(ATask.PathInclude) + 'Zydis.Strings.' + ATask.EnumName + '.inc';
  N := ATask.EnumName + 'Strings';
  S := ',';
  W := TStreamWriter.Create(F);
  try
    W.AutoFlush := true;
    W.NewLine := sLineBreak;
    W.Write('const');
    W.WriteLine;
    W.Write('  %s = (', [N]);
    W.WriteLine;
    for I := Low(AItems) to High(AItems) do
    begin
      if (I = High(AItems)) then
      begin
        S := '';
      end;
      W.Write('    ''%s''%s%s', [AItems[I], S, sLineBreak]);
      WorkStep(AGenerator);
    end;
    W.Write('  );');
    W.WriteLine;
  finally
    W.Free;
  end;
end;

class function TZYEnumGeneratorPascal.GetName: String;
begin
  Result := 'Pascal';
end;

class function TZYEnumGeneratorPascal.IsKeyword(const S: String): Boolean;
const
  KEYWORDS: array of String = [
    'absolute',
    'abstract',
    'alias',
    'and',
    'array',
    'as',
    'asm',
    'assembler',
    'begin',
    'break',
    'case',
    'cdecl',
    'class',
    'cppdecl',
    'const',
    'constructor',
    'continue',
    'default',
    'destructor',
    'dispose',
    'div',
    'do',
    'downto',
    'else',
    'end',
    'except',
    'exit',
    'export',
    'exports',
    'external',
    'false',
    'file',
    'finalization',
    'finally',
    'for',
    'forward',
    'function',
    'generic',
    'goto',
    'if',
    'implementation',
    'in',
    'index',
    'inherited',
    'initialization',
    'inline',
    'interface',
    'is',
    'label',
    'library',
    'local',
    'mod',
    'name',
    'new',
    'nil',
    'nostackframe',
    'not',
    'object',
    'of',
    'oldfpccall',
    'on',
    'operator',
    'or',
    'out',
    'override',
    'packed',
    'pascal',
    'private',
    'procedure',
    'program',
    'property',
    'protected',
    'public',
    'published',
    'raise',
    'read',
    'record',
    'register',
    'reintroduce',
    'repeat',
    'safecall',
    'self',
    'set',
    'shl',
    'shr',
    'softfloat',
    'specialize',
    'stdcall',
    'string',
    'then',
    'threadvar',
    'to',
    'true',
    'try',
    'type',
    'unit',
    'until',
    'uses',
    'var',
    'virtual',
    'while',
    'with',
    'write',
    'xor'
  ];
begin
  Result := System.StrUtils.AnsiIndexText(S, KEYWORDS) >= 0;
end;
{$ENDREGION}

{$REGION 'Class: TZYEnumGeneratorPython'}
class procedure TZYEnumGeneratorPython.Generate(AGenerator: TZYBaseGenerator;
  const ARootDirectory: String; const ATask: TZYEnumGeneratorTaskItem;
  const AItems: array of String);
begin
  // TODO:
end;

class function TZYEnumGeneratorPython.GetName: String;
begin
  Result := 'Python';
end;

class function TZYEnumGeneratorPython.IsKeyword(const S: String): Boolean;
begin
  Result := false;
  // TODO:
end;
{$ENDREGION}

{$REGION 'Class: TZYEnumGeneratorRust'}
class procedure TZYEnumGeneratorRust.Generate(AGenerator: TZYBaseGenerator;
  const ARootDirectory: String; const ATask: TZYEnumGeneratorTaskItem;
  const AItems: array of String);
begin
  // TODO:
end;

class function TZYEnumGeneratorRust.GetName: String;
begin
  Result := 'Rust';
end;

class function TZYEnumGeneratorRust.IsKeyword(const S: String): Boolean;
begin
  Result := false;
  // TODO:
end;
{$ENDREGION}

end.
