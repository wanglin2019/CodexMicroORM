﻿/***********************************************************************
Copyright 2018 CodeX Enterprises LLC

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

Major Changes:
06/2018    0.7     Initial release (Joel Champagne)
***********************************************************************/
using CodexMicroORM.Core.Services;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CodexMicroORM.Core
{
    public static class AttributeInitializer
    {
        public static void Apply(params Assembly[] args)
        {
            if (args?.Length == 0)
            {
                args = AppDomain.CurrentDomain.GetAssemblies();
            }

            // Traverse provided assemblies, looking for classes that implement attributes of interest
            Parallel.ForEach(args, (a) =>
            {
                Parallel.ForEach(a.GetTypes(), (t) =>
                {
                    var pkAttr = t.GetCustomAttribute<EntityPrimaryKeyAttribute>();

                    if (pkAttr != null)
                    {
                        typeof(KeyService).GetMethod("RegisterKey").MakeGenericMethod(t).Invoke(null, new object[] { pkAttr.Fields });

                        foreach (var prop in t.GetProperties())
                        {
                            var maxLenAttr = prop.GetCustomAttribute<EntityMaxLengthAttribute>();

                            if (maxLenAttr != null)
                            {
                                typeof(ValidationService).GetMethod("RegisterMaxLength").MakeGenericMethod(t).Invoke(null, new object[] { prop.Name, maxLenAttr.Length });
                            }

                            var defValAttr = prop.GetCustomAttribute<EntityDefaultValueAttribute>();

                            if (defValAttr != null)
                            {
                                typeof(DBService).GetMethod("RegisterDefault").MakeGenericMethod(t, prop.PropertyType).Invoke(null, new object[] { prop.Name, Convert.ChangeType(defValAttr.Value, prop.PropertyType) });
                            }
                        }
                    }

                    var schemaAttr = t.GetCustomAttribute<EntitySchemaNameAttribute>();

                    if (schemaAttr != null)
                    {
                        typeof(DBService).GetMethod("RegisterSchema").MakeGenericMethod(t).Invoke(null, new object[] { schemaAttr.Name });
                    }

                    var relAttr = t.GetCustomAttribute<EntityRelationshipsAttribute>();

                    if (relAttr != null)
                    {
                        typeof(KeyService).GetMethod("RegisterRelationship").MakeGenericMethod(t).Invoke(null, new object[] { relAttr.Relations });
                    }
                });
            });
        }
    }
}
