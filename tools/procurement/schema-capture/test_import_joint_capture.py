"""Pure mutation tests; these synthetic strings are NOT PostgreSQL capture evidence."""
import copy,json,re,unittest
from pathlib import Path
import import_joint_capture as m
class CaptureImportTests(unittest.TestCase):
    def setUp(self):
        self.sources={k:(m.ROOT/m.API/n).read_text() for k,n in m.CLASSES.items()};self.snap={};self.capture={}
        for phase,source in self.sources.items():
            rows=[]
            for field,kind in [('CheckNames','c'),('ForeignNames','f'),('ParentNames' if phase=='p36' else 'OverlayKeyNames','u')]:
                for full in sorted(m.names(source,field)):
                    schema,table,name=full.split('.');d=phase=='p37' and name=='fk_pgret_current_revision'
                    rows.append([schema,table,name,kind,{'c':'CHECK (unit_fixture)','f':'FOREIGN KEY (unit_fixture)','u':'UNIQUE (unit_fixture)'}[kind],True,d,d])
            table=next(iter(m.names(source,'CheckNames'))).split('.')[1]
            indexes=[['procurement',table,n,'CREATE INDEX unit_fixture'] for n in sorted(m.expected_indexes(phase,source))]
            self.snap[phase]={'catalog':{'constraints':rows,'indexes':indexes}}
            self.capture[phase]=m.canonical(self.snap[phase],phase,source)
    def test_source_inventories_and_empty_render(self):
        m.verify_capture(self.capture,self.snap,self.sources)
        for phase in m.CLASSES:
            template=re.sub(r'new Dictionary<string,string>\(StringComparer.Ordinal\)\n    \{.*?\n    \};','new Dictionary<string,string>(StringComparer.Ordinal);',self.sources[phase],flags=re.S)
            self.assertIn('unit_fixture',m.render(template,phase,self.capture[phase]))
    def test_missing_check(self):
        self.capture['p36']['checks'].pop(next(iter(self.capture['p36']['checks'])))
        with self.assertRaisesRegex(ValueError,'mismatch'):m.verify_capture(self.capture,self.snap,self.sources)
    def test_wrong_check(self):
        self.capture['p37']['checks'][next(iter(self.capture['p37']['checks']))]='CHECK (changed)'
        with self.assertRaisesRegex(ValueError,'mismatch'):m.verify_capture(self.capture,self.snap,self.sources)
    def test_wrong_key(self):
        self.capture['p36']['parentKeys'][next(iter(self.capture['p36']['parentKeys']))]['definition']='UNIQUE (wrong)'
        with self.assertRaisesRegex(ValueError,'mismatch'):m.verify_capture(self.capture,self.snap,self.sources)
    def test_wrong_deferrability_even_if_both_inputs_changed(self):
        row=next(r for r in self.snap['p37']['catalog']['constraints'] if r[2]=='fk_pgret_current_revision');row[6]=False
        with self.assertRaisesRegex(ValueError,'deferrability'):m.verify_capture(self.capture,self.snap,self.sources)
    def test_index_text_changed(self):
        self.capture['p37']['indexes'][0][3]='CREATE INDEX wrong'
        # Capture and snapshot share row instances in this synthetic setup; detach the trusted input.
        self.snap['p37']['catalog']['indexes']=copy.deepcopy(self.snap['p37']['catalog']['indexes']);self.snap['p37']['catalog']['indexes'][0][3]='CREATE INDEX unit_fixture'
        with self.assertRaisesRegex(ValueError,'mismatch'):m.verify_capture(self.capture,self.snap,self.sources)
    def test_unknown_index_even_if_both_inputs_changed(self):
        self.snap['p36']['catalog']['indexes'][0][2]='unknown'
        with self.assertRaisesRegex(ValueError,'index-inventory'):m.verify_capture(self.capture,self.snap,self.sources)
    def test_partial_or_wrong_named_fk(self):
        self.snap['p37']['catalog']['constraints']=[r for r in self.snap['p37']['catalog']['constraints'] if r[2]!='fk_pgret_current_revision']
        with self.assertRaisesRegex(ValueError,'inventory'):m.verify_capture(self.capture,self.snap,self.sources)
    def test_inert_plan_cannot_render(self):
        with self.assertRaisesRegex(ValueError,'reviewed-exact-lineage'):m.execute({'status':'INCOMPLETE'})
if __name__=='__main__':unittest.main()
