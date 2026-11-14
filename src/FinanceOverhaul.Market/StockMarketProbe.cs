using System;
using System.Collections.Generic;
using System.Reflection;

namespace FinanceOverhaul.Market
{
    /// <summary>
    /// Discovers stock-related types via reflection without referencing Cities
    /// assemblies. The mod surface supplies an <see cref="Assembly"/> at runtime.
    /// </summary>
    public static class StockMarketProbe
    {
        public sealed class DiscoveryResult
        {
            public string AssemblyName { get; set; }
            public IList<string> CandidateTypeNames { get; set; }
            public IList<string> CandidateMethodNames { get; set; }
        }

        public static DiscoveryResult Discover(Assembly assembly)
        {
            var result = new DiscoveryResult
            {
                AssemblyName = assembly == null ? null : assembly.GetName().Name,
                CandidateTypeNames = new List<string>(),
                CandidateMethodNames = new List<string>(),
            };

            if (assembly == null)
            {
                return result;
            }

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types ?? new Type[0];
            }

            for (int i = 0; i < types.Length; i++)
            {
                Type type = types[i];
                if (type == null || string.IsNullOrEmpty(type.FullName))
                {
                    continue;
                }

                if (!LooksLikeStockType(type.Name))
                {
                    continue;
                }

                result.CandidateTypeNames.Add(type.FullName);
                MethodInfo[] methods;
                try
                {
                    methods = type.GetMethods(
                        BindingFlags.Instance
                            | BindingFlags.Static
                            | BindingFlags.Public
                            | BindingFlags.NonPublic
                    );
                }
                catch
                {
                    continue;
                }

                for (int m = 0; m < methods.Length; m++)
                {
                    MethodInfo method = methods[m];
                    if (method == null || method.IsAbstract || method.ContainsGenericParameters)
                    {
                        continue;
                    }

                    if (LooksLikeUpdateMethod(method.Name))
                    {
                        result.CandidateMethodNames.Add(type.FullName + "::" + method.Name);
                    }
                }
            }

            return result;
        }

        public static bool LooksLikeStockType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return false;
            }

            return ContainsIgnoreCase(typeName, "Stock")
                || ContainsIgnoreCase(typeName, "Investment")
                || ContainsIgnoreCase(typeName, "SharePrice");
        }

        public static bool LooksLikeUpdateMethod(string methodName)
        {
            if (string.IsNullOrEmpty(methodName))
            {
                return false;
            }

            return ContainsIgnoreCase(methodName, "Update")
                || ContainsIgnoreCase(methodName, "Simulation")
                || ContainsIgnoreCase(methodName, "Tick")
                || ContainsIgnoreCase(methodName, "Price");
        }

        public static float ReadFloatMember(object target, string memberName)
        {
            if (target == null || string.IsNullOrEmpty(memberName))
            {
                return float.NaN;
            }

            Type type = target.GetType();
            FieldInfo field = type.GetField(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );
            if (field != null && field.FieldType == typeof(float))
            {
                return (float)field.GetValue(target);
            }

            PropertyInfo prop = type.GetProperty(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );
            if (
                prop != null
                && prop.PropertyType == typeof(float)
                && prop.CanRead
                && prop.GetIndexParameters().Length == 0
            )
            {
                return (float)prop.GetValue(target, null);
            }

            return float.NaN;
        }

        public static bool TryWriteFloatMember(object target, string memberName, float value)
        {
            if (target == null || string.IsNullOrEmpty(memberName))
            {
                return false;
            }

            Type type = target.GetType();
            FieldInfo field = type.GetField(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );
            if (field != null && field.FieldType == typeof(float) && !field.IsLiteral)
            {
                field.SetValue(target, value);
                return true;
            }

            PropertyInfo prop = type.GetProperty(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );
            if (
                prop != null
                && prop.PropertyType == typeof(float)
                && prop.CanWrite
                && prop.GetIndexParameters().Length == 0
            )
            {
                prop.SetValue(target, value, null);
                return true;
            }

            return false;
        }

        private static bool ContainsIgnoreCase(string haystack, string needle)
        {
            return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
