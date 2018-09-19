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

unit Zydis.Generator.Consts;

interface

uses
  Zydis.Enums.Filters;

type
  TZYGeneratorConsts = record
  public const
    PathIncludeC            = 'zydis\include\Zydis\Generated';
    PathSourceC             = 'zydis\src\Generated';
    PathIncludeCPP          = 'zydis-cpp\include\Zydis\Generated';
    PathSourceCPP           = 'zydis-cpp\src\Generated';
    PathIncludeJava         = 'zydis-java\Generated';
    PathSourceJava          = 'zydis-java\Generated';
    PathIncludePascal       = 'zydis-pascal\Zydis\Generated';
    PathSourcePascal        = 'zydis-pascal\Zydis\Generated';
    PathIncludePython       = 'zydis-py\Generated';
    PathSourcePython        = 'zydis-py\Generated';
    PathIncludeRust         = 'zydis-rs\src';
    PathSourceRust          = 'zydis-rs\src';
    PathInclude             = PathIncludeC;
    PathSource              = PathSourceC;
  public const
    FilenameInstructions    = 'InstructionDefinitions.inc';
    FilenameOperands        = 'OperandDefinitions.inc';
    FilenameEncodings       = 'InstructionEncodings.inc';
    FilenameAccessedFlags   = 'AccessedFlags.inc';
    FilenameDecoderTables   = 'DecoderTables.inc';
    FilenameEncoderTables   = 'EncoderTables.inc';
  public const
    SizeOfInstructionDefinition: array[TZYInstructionEncoding] of Cardinal = (
      { DEFAULT } 14,
      { 3DNOW   } 12,
      { XOP     } 13,
      { VEX     } 13,
      { EVEX    } 16,
      { MVEX    } 15
    );
    SizeOfOperandDefinition   = 11;
    SizeOfInstructionEncoding = 14;
    SizeOfAccessedFlags       = 21;
    SizeOfDecoderTreeNode     =  3;
  end;

implementation

end.
