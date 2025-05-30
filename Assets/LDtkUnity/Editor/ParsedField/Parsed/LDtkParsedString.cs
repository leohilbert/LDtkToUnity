﻿using JetBrains.Annotations;

namespace LDtkUnity.Editor
{
    [UsedImplicitly]
    internal sealed class LDtkParsedString : ILDtkValueParser
    {
        public object ImportString(LDtkFieldParseContext ctx)
        {
            object input = ctx.Input;
            //strings can be legally null
            if (input == null)
            {
                return default;
            }
            
            string stringInput = (string) input;

            return stringInput;
        }
    }
}