﻿using Newtonsoft.Json;
using Molten.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Molten
{
    public static class JsonHelper
    {
        static JsonConverter[] _customConverters;

        static JsonHelper()
        {
            _customConverters = new JsonConverter[]
            {
                new MathConverter(),
            };
        }

        public static JsonSerializerSettings GetDefaultSettings(Logger log)
        {
            return new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.None,
                Error = (sender, args) =>
                {
                    log.WriteError(args.ErrorContext.Error, true);
                    args.ErrorContext.Handled = true;
                },
                Converters = _customConverters,
                CheckAdditionalContent = false,
            };
        }

        public static JsonConverter[] GetEngineConverts()
        {
            return _customConverters.Clone() as JsonConverter[];
        }
    }
}
