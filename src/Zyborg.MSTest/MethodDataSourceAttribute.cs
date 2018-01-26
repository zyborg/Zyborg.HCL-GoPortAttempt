using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Zyborg.MSTest
{
    [AttributeUsage(AttributeTargets.Method)]
    public class MethodDataSourceAttribute : Attribute, ITestDataSource
    {
        // public static readonly MethodInfo NullDisplayNameMethod =
        //         typeof(MethodDataSourceAttribute).GetMethod(nameof(NullDisplayName),
        //                 BindingFlags.Static);

        public static readonly MethodInfo DataIndexDisplayNameMethod =
                typeof(MethodDataSourceAttribute).GetMethod(nameof(DataIndexDisplayName),
                        BindingFlags.Static | BindingFlags.Public);

        public MethodDataSourceAttribute(Type methodClass, string methodName,
                string displayMethodName = null)
        {
            this.MethodClass = methodClass;
            this.DataMethodName = methodName;
            this.DisplayMethodName = displayMethodName;
        }

        public MethodDataSourceAttribute(Type methodClass, string methodName,
                int displayDataIndex) : this(methodClass, methodName, null)
        {
            this.DisplayDataIndex = displayDataIndex;
            this.DisplayMethod = DataIndexDisplayNameMethod;
        }

        public Type MethodClass
        { get; }

        public string DataMethodName
        { get; }

        public string DisplayMethodName
        { get; }

        public int? DisplayDataIndex
        { get; }

        public MethodInfo DataMethod
        { get; private set; }

        public MethodInfo DisplayMethod
        { get; private set; }

        public MethodInfo TestMethod
        { get; private set; }

        public IEnumerable<object[]> GetData(MethodInfo methodInfo)
        {
            if (DataMethod == null)
            {
                DataMethod = MethodClass.GetMethod(DataMethodName, BindingFlags.Static
                        | BindingFlags.Public | BindingFlags.NonPublic);
                if (DataMethod == null)
                    throw new InvalidOperationException($"could not resolve test data source method:"
                            + $" class=[{MethodClass.FullName}]"
                            + $" method=[{DataMethodName}]");
            }
            if (TestMethod == null)
                TestMethod = methodInfo;

            object[] methodParams = null;
            if (DataMethod.GetParameters().Length > 0)
                methodParams = new object[] { this };

            return (IEnumerable<object[]>)DataMethod.Invoke(null, methodParams);
        }

        public string GetDisplayName(MethodInfo methodInfo, object[] data)
        {
            if (DisplayMethod == null)
            {
                if (string.IsNullOrEmpty(DisplayMethodName))
                    return null;

                DisplayMethod = MethodClass.GetMethod(DisplayMethodName, BindingFlags.Static
                        | BindingFlags.Public | BindingFlags.NonPublic);
                if (DisplayMethodName == null)
                    throw new InvalidOperationException($"could not resolve test data display method:"
                            + $" class=[{MethodClass.FullName}]"
                            + $" method=[{DisplayMethodName}]");
            }
            if (TestMethod == null)
                TestMethod = methodInfo;

            object[] methodParams = null;
            var paramInfos = DisplayMethod.GetParameters();
            var paramLen = paramInfos.Length;
            if (paramLen == 2 && paramInfos[1].ParameterType == typeof(object[]))
            {
                methodParams = new object[] { this, data };
            }
            else if (paramLen > 1)
            {
                methodParams = new object[data.Length + 1];
                methodParams[0] = this;
                Array.Copy(data, 0, methodParams, 1, data.Length);
            }
            else if (paramLen > 0)
            {
                methodParams = new object[] { this };
            }

            return (string)DisplayMethod.Invoke(null, methodParams);
        }

        // public static string NullDisplayName() => null;

        public static string DataIndexDisplayName(MethodDataSourceAttribute att,
                object[] data) =>
                $"{att.TestMethod.Name} ({data[att.DisplayDataIndex.Value]})";
    }
}
