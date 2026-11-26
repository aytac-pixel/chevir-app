import json,sys
sys.path.insert(0,'.')
from tr import TR
k=json.load(open('keys.json'))
def cs(s): return '"'+s.replace('\\','\\\\').replace('"','\\"').replace('\n','\\n')+'"'
rows=[]
for az,(en,ru) in TR.items():
    en = en or k.get(az,{}).get('en') or az
    rows.append((az,en,ru))
out=['// Word list for the English and Russian app texts, keyed by the Azerbaijani text used in code.',
'// Generated from Tools/i18n/tr.py; edit that file and run Tools/i18n/gen_translations.py.',
'using System.Collections.Generic;','','namespace Cevir.Core.Localization','{',
'    public static class Translations','    {',
'        // { az, en, ru }','        private static readonly string[][] Rows =','        {']
for az,en,ru in rows: out.append('            new[] { %s, %s, %s },'%(cs(az),cs(en),cs(ru or '')))
out+=['        };','',
'        private static Dictionary<string, string[]> _byAz;','        private static Dictionary<string, string[]> _byEn;','',
'        private static void Ensure()','        {','            if (_byAz != null) return;',
'            _byAz = new Dictionary<string, string[]>(Rows.Length);','            _byEn = new Dictionary<string, string[]>(Rows.Length);',
'            foreach (string[] row in Rows)','            {','                _byAz[row[0]] = row;','                if (!_byEn.ContainsKey(row[1])) _byEn[row[1]] = row;','            }','        }','',
'        /// <summary>Russian text for an Azerbaijani (or English) key, or null when there is none.</summary>',
'        public static string Russian(string key)','        {','            if (string.IsNullOrEmpty(key)) return null;','            Ensure();',
'            string[] row;','            if (!_byAz.TryGetValue(key, out row) && !_byEn.TryGetValue(key, out row)) return null;',
'            return string.IsNullOrEmpty(row[2]) ? null : row[2];','        }','',
'        /// <summary>English text for an Azerbaijani key, or null when there is none.</summary>',
'        public static string English(string az)','        {','            if (string.IsNullOrEmpty(az)) return null;','            Ensure();',
'            string[] row;','            return _byAz.TryGetValue(az, out row) ? row[1] : null;','        }','    }','}','']
open(sys.argv[1] if len(sys.argv)>1 else "../../Assets/Core/Scripts/Localization/Translations.cs",'w').write('\n'.join(out))
print(len(rows))
