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

unit Zydis.Generator.Enums;

interface

uses
  Zydis.Generator, Zydis.Generator.Types, System.SysUtils;

{$SCOPEDENUMS ON}

type
  TZYEnumGeneratorFlag = (
    {**
     * @brief Generates this enum in a private namespace.
     * }
    PrivateEnum,
    {**
     * @brief Generates internal string arrays for the native library enum.
     * }
    GenerateNativeStrings,
    {**
     * @brief Generates string arrays for the language bindings.
     * }
    GenerateBindingStrings,
    {**
     * @brief Uses the `ZydisString` datatype instead of the default `char*`.
     * }
    ZydisString
  );
  TZYEnumGeneratorFlags = set of TZYEnumGeneratorFlag;

  TZYEnumGeneratorLanguage = class;
  TZYEnumGeneratorLanguageClass = class of TZYEnumGeneratorLanguage;

  TZYEnumGenerator = class sealed(TZYGeneratorTask)
  strict private
    class var Languages: TArray<TZYEnumGeneratorLanguageClass>;
  private
    class procedure WorkStep(Generator: TZYCodeGenerator); static; inline;
  protected
    class procedure Register(AClass: TZYEnumGeneratorLanguageClass); static; inline;
  public
    class procedure Generate(Generator: TZYCodeGenerator;
      const RootDirectory, EnumName, ItemPrefix: String;
      Enum: TZYGeneratorEnum; Flags: TZYEnumGeneratorFlags = []); static;
  end;

  TZYEnumGeneratorLanguage = class abstract(TObject)
  strict protected
    class procedure WorkStep(Generator: TZYCodeGenerator); static; inline;
  protected
    class function GetName: String; virtual; abstract;
    class function IsNativeLanguage: Boolean; virtual;
    class function GetMaxWorkCount(Enum: TZYGeneratorEnum;
      GenerateStrings: Boolean): Integer; virtual;
    class procedure Generate(Generator: TZYCodeGenerator;
      const RootDirectory, EnumName, ItemPrefix: String; Enum: TZYGeneratorEnum;
      PrivateEnum, GenerateStrings, UseZydisString: Boolean); virtual; abstract;
  end;

implementation

uses
  System.Classes, System.StrUtils, System.Math, Zydis.Generator.Consts;

{$REGION 'Class: TZYEnumGeneratorC'}
type
  TZYEnumGeneratorC = class sealed(TZYEnumGeneratorLanguage)
  strict private
    class function IsKeyword(const S: String): Boolean; static;
  protected
    class function GetName: String; override;
    class function IsNativeLanguage: Boolean; override;
    class procedure Generate(Generator: TZYCodeGenerator;
      const RootDirectory, EnumName, ItemPrefix: String;
      Enum: TZYGeneratorEnum; PrivateEnum, GenerateStrings, UseZydisString: Boolean); override;
  end;

{ TZYEnumGeneratorC }

class procedure TZYEnumGeneratorC.Generate(Generator: TZYCodeGenerator; const RootDirectory,
  EnumName, ItemPrefix: String; Enum: TZYGeneratorEnum; PrivateEnum, GenerateStrings,
  UseZydisString: Boolean);
var
  F, N, S, T, U: String;
  W: TStreamWriter;
  I, X, Y: Integer;
begin
  F := IncludeTrailingPathDelimiter(RootDirectory);
  if (PrivateEnum) then
  begin
    F := F + IncludeTrailingPathDelimiter(TZYGeneratorConsts.PathSourceC) +
      'Enum' + EnumName + '.h';
  end else
  begin
    F := F + IncludeTrailingPathDelimiter(TZYGeneratorConsts.PathIncludeC) +
      'Enum' + EnumName + '.h';
  end;
  N := 'Zydis' + EnumName;
  if (N.EndsWith('y')) then
  begin
    Delete(N, Length(N), 1);
    N := N + 'ies';
  end else
  begin
    N := N + 's';
  end;
  S := ',';
  X := Floor(Log2(High(Enum.Items))) + 1;
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
      [sLineBreak, EnumName, sLineBreak, sLineBreak]);
    W.Write('typedef ZydisU%d Zydis%s;', [Y, EnumName]);
    W.WriteLine;
    W.WriteLine;
    W.Write('/**%s * @brief   Values that represent `Zydis%s` elements.%s */%s',
      [sLineBreak, EnumName, sLineBreak, sLineBreak]);
    W.Write('enum %s', [N]);
    W.WriteLine;
    W.Write('{');
    W.WriteLine;
    for I := Low(Enum.Items) to High(Enum.Items) do
    begin
      if (I = High(Enum.Items)) then
      begin
        S := '';
      end;
      T := Enum.Items[I].ToUpper;
      if (IsKeyword(T)) then
      begin
        Assert(false);
        T := '_' + T;
      end;
      W.Write('    %s%s%s%s%s', ['ZYDIS_', ItemPrefix, T, S, sLineBreak]);
      WorkStep(Generator);
    end;
    W.Write('};' + sLineBreak + sLineBreak);
    W.Write('#define %s%sMAX_VALUE %s%s%s%s', [
      'ZYDIS_', ItemPrefix, 'ZYDIS_', ItemPrefix, T, sLineBreak]);
    W.Write('#define %s%sMAX_BITS  0x%.4X', ['ZYDIS_', ItemPrefix, X]);
    W.WriteLine;
  finally
    W.Free;
  end;
  if (not GenerateStrings) then
  begin
    Exit;
  end;
  N := 'zydis' + EnumName + 'Strings';
  F := IncludeTrailingPathDelimiter(RootDirectory) +
    IncludeTrailingPathDelimiter(TZYGeneratorConsts.PathSourceC) + 'Enum' + EnumName + '.inc';
  S := ',';
  U := 'char*';
  if (UseZydisString) then
  begin
    U := 'ZydisGeneratedString';
  end;
  W := TStreamWriter.Create(F);
  try
    W.AutoFlush := true;
    W.NewLine := sLineBreak;
    W.Write('static const %s %s[] =', [U, N]);
    W.WriteLine;
    W.Write('{');
    W.WriteLine;
    for I := Low(Enum.Items)  to High(Enum.Items)  do
    begin
      if (I = High(Enum.Items) ) then
      begin
        S := '';
      end;
      if (UseZydisString) then
      begin
        W.Write('    ZYDIS_MAKE_GENERATED_STRING("%s")%s%s', [Enum.Items[I], S, sLineBreak]);
      end else
      begin
        W.Write('    "%s"%s%s', [Enum.Items[I], S, sLineBreak]);
      end;
      WorkStep(Generator);
    end;
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

class function TZYEnumGeneratorC.IsNativeLanguage: Boolean;
begin
  Result := true;
end;
{$ENDREGION}

{$REGION 'Class: TZYEnumGeneratorCPP'}
type
  TZYEnumGeneratorCPP = class sealed(TZYEnumGeneratorLanguage)
  strict private
    class function IsKeyword(const S: String): Boolean; static;
  protected
    class function GetName: String; override;
    class procedure Generate(Generator: TZYCodeGenerator;
      const RootDirectory, EnumName, ItemPrefix: String;
      Enum: TZYGeneratorEnum; PrivateEnum, GenerateStrings, UseZydisString: Boolean); override;
  end;

{ TZYEnumGeneratorCPP }

class procedure TZYEnumGeneratorCPP.Generate(Generator: TZYCodeGenerator; const RootDirectory,
  EnumName, ItemPrefix: String; Enum: TZYGeneratorEnum; PrivateEnum, GenerateStrings,
  UseZydisString: Boolean);
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
type
  TZYEnumGeneratorJava = class sealed(TZYEnumGeneratorLanguage)
  strict private
    class function IsKeyword(const S: String): Boolean; static;
  protected
    class function GetName: String; override;
    class procedure Generate(Generator: TZYCodeGenerator;
      const RootDirectory, EnumName, ItemPrefix: String;
      Enum: TZYGeneratorEnum; PrivateEnum, GenerateStrings, UseZydisString: Boolean); override;
  end;

{ TZYEnumGeneratorJava }

class procedure TZYEnumGeneratorJava.Generate(Generator: TZYCodeGenerator; const RootDirectory,
  EnumName, ItemPrefix: String; Enum: TZYGeneratorEnum; PrivateEnum, GenerateStrings,
  UseZydisString: Boolean);
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
type
  TZYEnumGeneratorPascal = class sealed(TZYEnumGeneratorLanguage)
  strict private
    class function IsKeyword(const S: String): Boolean; static;
  protected
    class function GetName: String; override;
    class procedure Generate(Generator: TZYCodeGenerator;
      const RootDirectory, EnumName, ItemPrefix: String;
      Enum: TZYGeneratorEnum; PrivateEnum, GenerateStrings, UseZydisString: Boolean); override;
  end;

{ TZYEnumGeneratorPascal }

class procedure TZYEnumGeneratorPascal.Generate(Generator: TZYCodeGenerator; const RootDirectory,
  EnumName, ItemPrefix: String; Enum: TZYGeneratorEnum; PrivateEnum, GenerateStrings,
  UseZydisString: Boolean);
var
  F, N, S, T: String;
  W: TStreamWriter;
  I: Integer;
begin
  F := IncludeTrailingPathDelimiter(RootDirectory) + 
    IncludeTrailingPathDelimiter(TZYGeneratorConsts.PathIncludePascal) +
    'Zydis.Enum.' + EnumName + '.inc';
  N := 'TZydis' + EnumName;
  S := ',';
  W := TStreamWriter.Create(F);
  try
    W.AutoFlush := true;
    W.NewLine := sLineBreak;
    W.Write('type');
    W.WriteLine;
    W.Write('  %s = (', [N]);
    W.WriteLine;
    for I := Low(Enum.Items)  to High(Enum.Items)  do
    begin
      if (I = High(Enum.Items) ) then
      begin
        S := '';
      end;
      T := Enum.Items[I].ToUpper;
      if (IsKeyword(T)) then
      begin
        T := '&' + T;
      end;
      W.Write('    %s%s%s', [T, S, sLineBreak]);
      WorkStep(Generator);
    end;
    W.Write('  );');
    W.WriteLine;
  finally
    W.Free;
  end;
  if (not GenerateStrings) then
  begin
    Exit;
  end;
  F := IncludeTrailingPathDelimiter(RootDirectory) + 
    IncludeTrailingPathDelimiter(TZYGeneratorConsts.PathIncludePascal) +
    'Zydis.Strings.' + EnumName + '.inc';
  N := EnumName + 'Strings';
  S := ',';
  W := TStreamWriter.Create(F);
  try
    W.AutoFlush := true;
    W.NewLine := sLineBreak;
    W.Write('const');
    W.WriteLine;
    W.Write('  %s = (', [N]);
    W.WriteLine;
    for I := Low(Enum.Items)  to High(Enum.Items)  do
    begin
      if (I = High(Enum.Items) ) then
      begin
        S := '';
      end;
      W.Write('    ''%s''%s%s', [Enum.Items[I], S, sLineBreak]);
      WorkStep(Generator);
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
type
  TZYEnumGeneratorPython = class sealed(TZYEnumGeneratorLanguage)
  strict private
    class function IsKeyword(const S: String): Boolean; static;
  protected
    class function GetName: String; override;
    class procedure Generate(Generator: TZYCodeGenerator;
      const RootDirectory, EnumName, ItemPrefix: String;
      Enum: TZYGeneratorEnum; PrivateEnum, GenerateStrings, UseZydisString: Boolean); override;
  end;

{ TZYEnumGeneratorPython }

class procedure TZYEnumGeneratorPython.Generate(Generator: TZYCodeGenerator; const RootDirectory,
  EnumName, ItemPrefix: String; Enum: TZYGeneratorEnum; PrivateEnum, GenerateStrings,
  UseZydisString: Boolean);
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
type
  TZYEnumGeneratorRust = class sealed(TZYEnumGeneratorLanguage)
  strict private
    class function IsKeyword(const S: String): Boolean; static;
  protected
    class function GetName: String; override;
    class procedure Generate(Generator: TZYCodeGenerator;
      const RootDirectory, EnumName, ItemPrefix: String;
      Enum: TZYGeneratorEnum; PrivateEnum, GenerateStrings, UseZydisString: Boolean); override;
  end;

{ TZYEnumGeneratorRust }

class procedure TZYEnumGeneratorRust.Generate(Generator: TZYCodeGenerator; const RootDirectory,
  EnumName, ItemPrefix: String; Enum: TZYGeneratorEnum; PrivateEnum, GenerateStrings,
  UseZydisString: Boolean);
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

{$REGION 'Class: TZYEnumGenerator'}
class procedure TZYEnumGenerator.Generate(Generator: TZYCodeGenerator; const RootDirectory,
  EnumName, ItemPrefix: String; Enum: TZYGeneratorEnum; Flags: TZYEnumGeneratorFlags);
var
  I, N: Integer;
  B, C, D: Boolean;
begin
  N := 0;
  for I := Low(Languages) to High(Languages) do
  begin
    C :=
      (Languages[I].IsNativeLanguage and
      (TZYEnumGeneratorFlag.GenerateNativeStrings in Flags)) or
      ((not Languages[I].IsNativeLanguage) and
      (TZYEnumGeneratorFlag.GenerateBindingStrings in Flags));
    Inc(N, Languages[I].GetMaxWorkCount(Enum, C));
  end;
  WorkStart(Generator, N);
  for I := Low(Languages) to High(Languages) do
  begin
    B := TZYEnumGeneratorFlag.PrivateEnum in Flags;
    C :=
      (Languages[I].IsNativeLanguage and
      (TZYEnumGeneratorFlag.GenerateNativeStrings in Flags)) or
      ((not Languages[I].IsNativeLanguage) and
      (TZYEnumGeneratorFlag.GenerateBindingStrings in Flags));
    Assert(Languages[I].IsNativeLanguage or (not B));
    D := (TZYEnumGeneratorFlag.ZydisString in Flags);
    Languages[I].Generate(Generator, RootDirectory, EnumName, ItemPrefix, Enum, B, C, D);
  end;
  WorkEnd(Generator);
end;

class procedure TZYEnumGenerator.Register(AClass: TZYEnumGeneratorLanguageClass);
begin
  SetLength(Languages, Length(Languages) + 1);
  Languages[High(Languages)] := AClass;
end;

class procedure TZYEnumGenerator.WorkStep(Generator: TZYCodeGenerator);
begin
  TZYGeneratorTask.WorkStep(Generator);
end;
{$ENDREGION}

{$REGION 'Class: TZYEnumGeneratorLanguage'}
class function TZYEnumGeneratorLanguage.GetMaxWorkCount(Enum: TZYGeneratorEnum;
  GenerateStrings: Boolean): Integer;
begin
  Result := Length(Enum.Items);
  if (GenerateStrings) then
  begin
    Result := Result * 2;
  end;
end;

class function TZYEnumGeneratorLanguage.IsNativeLanguage: Boolean;
begin
  Result := false;
end;

class procedure TZYEnumGeneratorLanguage.WorkStep(Generator: TZYCodeGenerator);
begin
  TZYEnumGenerator.WorkStep(Generator);
end;
{$ENDREGION}

initialization
  TZYEnumGenerator.Register(TZYEnumGeneratorC);
  TZYEnumGenerator.Register(TZYEnumGeneratorCPP);
  TZYEnumGenerator.Register(TZYEnumGeneratorJava);
  TZYEnumGenerator.Register(TZYEnumGeneratorPascal);
  TZYEnumGenerator.Register(TZYEnumGeneratorPython);
  TZYEnumGenerator.Register(TZYEnumGeneratorRust);

end.
