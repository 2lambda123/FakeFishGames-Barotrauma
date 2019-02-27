﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Barotrauma
{
    class TextPack
    {
        public readonly string Language;

        private Dictionary<string, List<string>> texts;

        private string filePath;

        public TextPack(string filePath)
        {
            this.filePath = filePath;
            texts = new Dictionary<string, List<string>>();

            XDocument doc = XMLExtensions.TryLoadXml(filePath);
            if (doc == null || doc.Root == null) return;

            Language = doc.Root.GetAttributeString("language", "Unknown");

            foreach (XElement subElement in doc.Root.Elements())
            {
                string infoName = subElement.Name.ToString().ToLowerInvariant();
                if (!texts.TryGetValue(infoName, out List<string> infoList))
                {
                    infoList = new List<string>();
                    texts.Add(infoName, infoList);
                }

                infoList.Add(subElement.ElementInnerText());
            }
        }

        public string Get(string textTag)
        {
            if (!texts.TryGetValue(textTag.ToLowerInvariant(), out List<string> textList) || !textList.Any())
            {
                return null;
            }

            string text = textList[Rand.Int(textList.Count)].Replace(@"\n", "\n");
            return text;
        }

        public List<string> GetAll(string textTag)
        {
            if (!texts.TryGetValue(textTag.ToLowerInvariant(), out List<string> textList) || !textList.Any())
            {
                return null;
            }

            return textList;
        }

#if DEBUG
        public void CheckForDuplicates(int index)
        {
            Dictionary<string, int> textCounts = new Dictionary<string, int>();

            XDocument doc = XMLExtensions.TryLoadXml(filePath);
            if (doc == null || doc.Root == null) return;

            foreach (XElement subElement in doc.Root.Elements())
            {
                string infoName = subElement.Name.ToString().ToLowerInvariant();
                if (!textCounts.ContainsKey(infoName))
                {
                    textCounts.Add(infoName, 1);
                }
                else
                {
                    textCounts[infoName] += 1;
                }
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("Language: " + Language);
            sb.AppendLine();
            sb.Append("Duplicate entries:");
            sb.AppendLine();
            sb.AppendLine();

            for (int i = 0; i < textCounts.Keys.Count; i++)
            {
                if (textCounts[texts.Keys.ElementAt(i)] > 1)
                {
                    sb.Append(texts.Keys.ElementAt(i) + " Count: " + textCounts[texts.Keys.ElementAt(i)]);
                    sb.AppendLine();
                }
            }

            System.IO.StreamWriter file = new System.IO.StreamWriter(@"duplicate_" + Language.ToLower() + "_" + index + ".txt");
            file.WriteLine(sb.ToString());
            file.Close();
        }

        public void WriteToCSV(int index)
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < texts.Count; i++)
            {
                string key = texts.Keys.ElementAt(i);
                texts.TryGetValue(key, out List<string> infoList);
                
                for (int j = 0; j < infoList.Count; j++)
                {
                    sb.Append(key); // ID
                    sb.Append('*');
                    sb.Append(infoList[j]); // Original
                    sb.Append('*');
                    // Translated
                    sb.Append('*');
                    // Comments
                    sb.AppendLine();
                }
            }

            System.IO.StreamWriter file = new System.IO.StreamWriter(@"csv_" + Language.ToLower() + "_" + index + ".csv");
            file.WriteLine(sb.ToString());
            file.Close();
        }
#endif
    }
}
