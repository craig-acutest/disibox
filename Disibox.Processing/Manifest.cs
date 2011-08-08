﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Disibox.Utils;

namespace Disibox.Processing
{
    public static class Manifest
    {
        private static IDictionary<string, ITool> _procTools;

        /// <summary>
        /// Contains the names of the tools that work with all content types.
        /// </summary>
        private static IList<string> _multiPurposeTools;

        /// <summary>
        /// The key is the content type, the value is the tool name.
        /// </summary>
        private static ILookup<string, string> _availableTools;

        static Manifest()
        {
            InitTools();
        }

        public static void Main()
        {
            Console.WriteLine("Available tools:");
            foreach (var entry in _procTools)
                Console.WriteLine("* " + entry.Key);

            // Empty line
            Console.WriteLine();

            Console.WriteLine("Multipurpose tools:");
            foreach (var toolName in _multiPurposeTools)
                Console.WriteLine("* " + toolName);

            // Empty line
            Console.WriteLine();

            Console.WriteLine("Specific tools:");
            foreach (var entry in _availableTools)
                Console.WriteLine("* " + entry.Key);

            // Empty line
            Console.WriteLine();

            Console.WriteLine("Press any key to exit...");
            Console.Read();
        }

        public static IList<string> GetAvailableTools(string fileContentType)
        {
            //var availableTools = _availableTools[fileContentType];
            return _multiPurposeTools;//.Concat(availableTools).ToList();
        }

        public static ITool GetTool(string toolName)
        {
            ITool tool;
            _procTools.TryGetValue(toolName, out tool);
            return tool;
        }

        private static void InitTools()
        {
            _procTools = new Dictionary<string, ITool>();
            _multiPurposeTools = new List<string>();
            var tmpAvailableTools = new List<Pair<string, string>>();

            var toolsAssemblyPath = Properties.Settings.Default.ToolsAssemblyPath;
            var procAssembly = Assembly.LoadFile(toolsAssemblyPath);
            var procTypes = procAssembly.GetTypes();

            var iToolType = Type.GetType("Disibox.Processing.ITool");

            foreach (var toolType in procTypes)
            {
                // We require the tool to implement the ITool interface.
                var interfaces = toolType.GetInterfaces();
                if (!toolType.GetInterfaces().Contains(iToolType)) continue;

                var tool = (ITool) Activator.CreateInstance(toolType);
                var toolId = toolType.ToString();

                _procTools.Add(toolId, tool);

                if (tool.ProcessableTypes.Count() == 0)
                {
                    _multiPurposeTools.Add(toolId);
                    continue;
                }

                foreach (var contentType in tool.ProcessableTypes)
                {
                    var tmpPair = new Pair<string, string>(contentType, toolId);
                    tmpAvailableTools.Add(tmpPair);
                }
            }

            _availableTools = tmpAvailableTools.ToLookup(p => p.First, p => p.Second);
        }
    }
}