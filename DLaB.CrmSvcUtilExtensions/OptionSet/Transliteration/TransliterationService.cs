﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace DLaB.CrmSvcUtilExtensions.OptionSet.Transliteration
{
    public static class TransliterationService
    {
        private static readonly string Path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "alphabets");

        private static readonly List<TransliterationAlphabet> Alphabets = new List<TransliterationAlphabet>();
        public static Lazy<List<int>> AvailableCodes { get; } =
            new Lazy<List<int>>(() =>
                Directory.GetFiles(Path)
                .Select(System.IO.Path.GetFileName)
                .Select(x => x.Split('.')[0])
                .Select(int.Parse).ToList());

        public static string Transliterate(int languageCode, string label)
        {
            var alphabet = 
                Alphabets.SingleOrDefault(x => x.LanguageCode == languageCode) ?? LoadAlphabet(languageCode);

            return alphabet.Transliterate(label);
        }

        private static TransliterationAlphabet LoadAlphabet(int languageCode)
        {
            var alphabetJson = 
                JObject.Parse(
                    File.ReadAllText(
                        System.IO.Path.Combine(Path, languageCode + ".json")));

            //var dictionary = new Dictionary<char, string>();
            var dictionary = 
                alphabetJson["alphabet"]
                .ToDictionary(
                    x => (char)x[0], 
                    x => (string)x[1]);

            var alphabet = new TransliterationAlphabet(languageCode, dictionary);

            Alphabets.Add(alphabet);

            return alphabet;
        }
    }
}
