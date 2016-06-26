﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Bit.App.Models
{
    // ref: https://github.com/danesparza/domainname-parser
    public class DomainName
    {
        private string _subDomain = string.Empty;
        private string _domain = string.Empty;
        private string _tld = string.Empty;
        private TLDRule _tldRule = null;

        public string SubDomain => _subDomain;
        public string Domain => _domain;
        public string SLD => _domain;
        public string TLD => _tld;
        public TLDRule Rule => _tldRule;

        public DomainName(string TLD, string SLD, string SubDomain, TLDRule TLDRule)
        {
            _tld = TLD;
            _domain = SLD;
            _subDomain = SubDomain;
            _tldRule = TLDRule;
        }

        public static bool TryParse(string domainString, out DomainName result)
        {
            bool retval = false;

            //  Our temporary domain parts:
            var tld = string.Empty;
            var sld = string.Empty;
            var subdomain = string.Empty;
            TLDRule _tldrule = null;
            result = null;

            try
            {
                //  Try parsing the domain name ... this might throw formatting exceptions
                ParseDomainName(domainString, out tld, out sld, out subdomain, out _tldrule);
                //  Construct a new DomainName object and return it
                result = new DomainName(tld, sld, subdomain, _tldrule);
                //  Return 'true'
                retval = true;
            }
            catch
            {
                //  Looks like something bad happened -- return 'false'
                retval = false;
            }

            return retval;
        }

        private static void ParseDomainName(string domainString, out string TLD, out string SLD, out string SubDomain, out TLDRule MatchingRule)
        {
            // Make sure domain is all lowercase
            domainString = domainString.ToLower();

            TLD = string.Empty;
            SLD = string.Empty;
            SubDomain = string.Empty;
            MatchingRule = null;

            //  If the fqdn is empty, we have a problem already
            if(domainString.Trim() == string.Empty)
            {
                throw new ArgumentException("The domain cannot be blank");
            }

            //  Next, find the matching rule:
            MatchingRule = FindMatchingTLDRule(domainString);

            //  At this point, no rules match, we have a problem
            if(MatchingRule == null)
            {
                throw new FormatException("The domain does not have a recognized TLD");
            }

            //  Based on the tld rule found, get the domain (and possibly the subdomain)
            var tempSudomainAndDomain = string.Empty;
            var tldIndex = 0;

            //  First, determine what type of rule we have, and set the TLD accordingly
            switch(MatchingRule.Type)
            {
                case TLDRule.RuleType.Normal:
                    tldIndex = domainString.LastIndexOf("." + MatchingRule.Name);
                    tempSudomainAndDomain = domainString.Substring(0, tldIndex);
                    TLD = domainString.Substring(tldIndex + 1);
                    break;
                case TLDRule.RuleType.Wildcard:
                    //  This finds the last portion of the TLD...
                    tldIndex = domainString.LastIndexOf("." + MatchingRule.Name);
                    tempSudomainAndDomain = domainString.Substring(0, tldIndex);

                    //  But we need to find the wildcard portion of it:
                    tldIndex = tempSudomainAndDomain.LastIndexOf(".");
                    tempSudomainAndDomain = domainString.Substring(0, tldIndex);
                    TLD = domainString.Substring(tldIndex + 1);
                    break;
                case TLDRule.RuleType.Exception:
                    tldIndex = domainString.LastIndexOf(".");
                    tempSudomainAndDomain = domainString.Substring(0, tldIndex);
                    TLD = domainString.Substring(tldIndex + 1);
                    break;
            }

            //  See if we have a subdomain:
            List<string> lstRemainingParts = new List<string>(tempSudomainAndDomain.Split('.'));

            //  If we have 0 parts left, there is just a tld and no domain or subdomain
            //  If we have 1 part, it's the domain, and there is no subdomain
            //  If we have 2+ parts, the last part is the domain, the other parts (combined) are the subdomain
            if(lstRemainingParts.Count > 0)
            {
                //  Set the domain:
                SLD = lstRemainingParts[lstRemainingParts.Count - 1];

                //  Set the subdomain, if there is one to set:
                if(lstRemainingParts.Count > 1)
                {
                    //  We strip off the trailing period, too
                    SubDomain = tempSudomainAndDomain.Substring(0, tempSudomainAndDomain.Length - SLD.Length - 1);
                }
            }
        }

        private static TLDRule FindMatchingTLDRule(string domainString)
        {
            //  Split our domain into parts (based on the '.')
            //  ...Put these parts in a list
            //  ...Make sure these parts are in reverse order (we'll be checking rules from the right-most pat of the domain)
            var lstDomainParts = domainString.Split('.').ToList();
            lstDomainParts.Reverse();

            //  Begin building our partial domain to check rules with:
            var checkAgainst = string.Empty;

            //  Our 'matches' collection:
            var ruleMatches = new List<TLDRule>();

            foreach(string domainPart in lstDomainParts)
            {
                //  Add on our next domain part:
                checkAgainst = string.Format("{0}.{1}", domainPart, checkAgainst);

                //  If we end in a period, strip it off:
                if(checkAgainst.EndsWith("."))
                {
                    checkAgainst = checkAgainst.Substring(0, checkAgainst.Length - 1);
                }

                var rules = Enum.GetValues(typeof(TLDRule.RuleType)).Cast<TLDRule.RuleType>();
                foreach(var rule in rules)
                {
                    //  Try to match rule:
                    TLDRule result;
                    if(TLDRulesCache.Instance.TLDRuleLists[rule].TryGetValue(checkAgainst, out result))
                    {
                        ruleMatches.Add(result);
                    }
                    Debug.WriteLine(string.Format("Domain part {0} matched {1} {2} rules", checkAgainst, result == null ? 0 : 1, rule));
                }
            }

            //  Sort our matches list (longest rule wins, according to :
            var results = from match in ruleMatches
                          orderby match.Name.Length descending
                          select match;

            //  Take the top result (our primary match):
            TLDRule primaryMatch = results.Take(1).SingleOrDefault();
            if(primaryMatch != null)
            {
                Debug.WriteLine(string.Format("Looks like our match is: {0}, which is a(n) {1} rule.", primaryMatch.Name, primaryMatch.Type));
            }
            else
            {
                Debug.WriteLine(string.Format("No rules matched domain: {0}", domainString));
            }

            return primaryMatch;
        }

        public class TLDRule : IComparable<TLDRule>
        {
            public string Name { get; private set; }
            public RuleType Type { get; private set; }

            public TLDRule(string RuleInfo)
            {
                //  Parse the rule and set properties accordingly:
                if(RuleInfo.StartsWith("*"))
                {
                    Type = RuleType.Wildcard;
                    Name = RuleInfo.Substring(2);
                }
                else if(RuleInfo.StartsWith("!"))
                {
                    Type = RuleType.Exception;
                    Name = RuleInfo.Substring(1);
                }
                else
                {
                    Type = RuleType.Normal;
                    Name = RuleInfo;
                }
            }

            public int CompareTo(TLDRule other)
            {
                if(other == null)
                {
                    return -1;
                }

                return Name.CompareTo(other.Name);
            }

            public enum RuleType
            {
                Normal,
                Wildcard,
                Exception
            }
        }

        public class TLDRulesCache
        {
            private static volatile TLDRulesCache _uniqueInstance;
            private static object _syncObj = new object();
            private static object _syncList = new object();

            private IDictionary<TLDRule.RuleType, IDictionary<string, TLDRule>> _lstTLDRules;

            private TLDRulesCache()
            {
                //  Initialize our internal list:
                _lstTLDRules = GetTLDRules();
            }

            public static TLDRulesCache Instance
            {
                get
                {
                    if(_uniqueInstance == null)
                    {
                        lock(_syncObj)
                        {
                            if(_uniqueInstance == null)
                            {
                                _uniqueInstance = new TLDRulesCache();
                            }
                        }
                    }
                    return (_uniqueInstance);
                }
            }

            public IDictionary<TLDRule.RuleType, IDictionary<string, TLDRule>> TLDRuleLists
            {
                get
                {
                    return _lstTLDRules;
                }
                set
                {
                    _lstTLDRules = value;
                }
            }

            public static void Reset()
            {
                lock(_syncObj)
                {
                    _uniqueInstance = null;
                }
            }

            private IDictionary<TLDRule.RuleType, IDictionary<string, TLDRule>> GetTLDRules()
            {
                var results = new Dictionary<TLDRule.RuleType, IDictionary<string, TLDRule>>();
                var rules = Enum.GetValues(typeof(TLDRule.RuleType)).Cast<TLDRule.RuleType>();
                foreach(var rule in rules)
                {
                    results[rule] = new Dictionary<string, TLDRule>(StringComparer.CurrentCultureIgnoreCase);
                }

                var ruleStrings = ReadRulesData();

                //  Strip out any lines that are:
                //  a.) A comment
                //  b.) Blank
                foreach(var ruleString in ruleStrings.Where(ruleString => !ruleString.StartsWith("//") && ruleString.Trim().Length != 0))
                {
                    var result = new TLDRule(ruleString);
                    results[result.Type][result.Name] = result;
                }

                //  Return our results:
                Debug.WriteLine(string.Format("Loaded {0} rules into cache.", results.Values.Sum(r => r.Values.Count)));
                return results;
            }

            private IEnumerable<string> ReadRulesData()
            {
                var assembly = typeof(TLDRulesCache).GetTypeInfo().Assembly;
                var stream = assembly.GetManifestResourceStream("Bit.App.Resources.public_suffix_list.dat");
                string line;
                using(var reader = new StreamReader(stream))
                {
                    while((line = reader.ReadLine()) != null)
                    {
                        yield return line;
                    }
                }
            }
        }
    }
}
