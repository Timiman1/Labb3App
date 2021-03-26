using System;
using System.Collections.Generic;
using System.Text;

namespace Labb3_Console
{
    public class PNG_Chunk
    {
        public string FieldType { get; }
        public int StartIndex { get; set; }
        public int Length { get; set; }

        public PNG_Chunk(string fType, int startIndex = -1, int length = -1)
        {
            FieldType = fType;
            StartIndex = startIndex;
            Length = length;
        }
    }
}
