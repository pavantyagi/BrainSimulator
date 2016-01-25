﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using GoodAI.Core.Configuration;
using GoodAI.Core.Utils;
using WeifenLuo.WinFormsUI.Docking;

namespace GoodAI.BrainSimulator.Forms
{
    [AttributeUsage(AttributeTargets.Class)]
    public class BrainSimUIExtensionAttribute : Attribute { }

    public static class UIPlugins
    {
        internal static IEnumerable<DockContent> GetBrainSimUIExtensions()
        {
            return GetBrainSimUIExtensionTypes().Select(type =>
                (DockContent) type.GetConstructor(new Type[] {}).Invoke(new object[] {}));
        }

        private static IEnumerable<Type> GetBrainSimUIExtensionTypes()
        {
            var ret = new List<Type>();

            foreach (FileInfo assemblyFile in MyConfiguration.ListModules())
            {
                Assembly assembly = Assembly.LoadFrom(assemblyFile.FullName);
                string xml = MyResources.GetTextFromAssembly(assembly, "nodes.xml");    //MyModuleConfig.MODULE_CONFIG_FILE instead of "nodes.xml"
                if (!string.IsNullOrEmpty(xml)) //skip nodes DLLs - GUI and logic should be divided
                    continue;

                ret.AddRange(assembly.GetTypes().Where(IsUIExtension));
            }

            return ret;
        }

        private static bool IsUIExtension(Type type)
        {
            return Attribute.GetCustomAttribute(type, typeof (BrainSimUIExtensionAttribute)) != null &&
                   type.IsSubclassOf(typeof (DockContent));
        }
    }
}