﻿using System;
using System.Reflection;
using System.Xml.Linq;

namespace Barotrauma
{
    class ScriptedEventPrefab
    {
        public readonly XElement ConfigElement;
        
        public readonly Type EventType;
        
        public readonly string MusicType;

        public ScriptedEventPrefab(XElement element)
        {
            ConfigElement = element;
         
            MusicType = element.GetAttributeString("musictype", "default");

            try
            {
                EventType = Type.GetType("Barotrauma." + ConfigElement.Name, true, true);
                if (EventType == null)
                {
                    DebugConsole.ThrowError("Could not find an event class of the type \"" + ConfigElement.Name + "\".");
                }
            }
            catch
            {
                DebugConsole.ThrowError("Could not find an event class of the type \"" + ConfigElement.Name + "\".");
            }
        }

        public ScriptedEvent CreateInstance()
        {
            ConstructorInfo constructor = EventType.GetConstructor(new[] { typeof(ScriptedEventPrefab) });
            object instance = null;
            try
            {
                instance = constructor.Invoke(new object[] { this });
            }
            catch (Exception ex)
            {
                DebugConsole.ThrowError(ex.InnerException != null ? ex.InnerException.ToString() : ex.ToString());
            }

            return (ScriptedEvent)instance;
        }
    }
}
