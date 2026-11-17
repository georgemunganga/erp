"""Prepare a version-pinned mobile-only compatibility build; never edit a live checkout.

The installed September model predates federated identity/Core organisation. Reuse
the current mobile boundary and attendance command, adapting only that model's
local-only credentials, explicit timezone configuration and existing primary keys.
"""
from pathlib import Path
import argparse,hashlib,json,subprocess,tarfile

BASE='6079337e7a17107ad0b33fca59a267f6efc6927a'
root=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser();parser.add_argument('--output',required=True);args=parser.parse_args()
out=Path(args.output).resolve();out.mkdir();src=out/'source';src.mkdir()
with (out/'baseline.tar').open('xb') as f:subprocess.run(['git','-C',str(root),'archive','--format=tar',BASE],stdout=f,check=True)
with tarfile.open(out/'baseline.tar') as t:
 for x in t.getmembers():
  if not (src/x.name).resolve().is_relative_to(src) or not(x.isdir() or x.isfile()):raise RuntimeError('Unexpected archive member')
 t.extractall(src,filter='data')
changes=[]
def read(path):return (src/path).read_text(encoding='utf-8-sig')
def write(path,text):
 p=src/path;old=p.read_bytes() if p.exists() else b'';p.parent.mkdir(parents=True,exist_ok=True);p.write_text(text,encoding='utf8')
 changes.append({'path':path,'before_sha256':hashlib.sha256(old).hexdigest(),'after_sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
def replace(text,old,new):
 if text.count(old)!=1:raise RuntimeError('Unsupported baseline shape: '+old[:75])
 return text.replace(old,new,1)
api='backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/'
app='backend/hrm-api/src/Mightyfin.Erp.Hrm.Application/'
infra='backend/hrm-api/src/Mightyfin.Erp.Hrm.Infrastructure/'
domain='backend/hrm-api/src/Mightyfin.Erp.Hrm.Domain/'
tests='backend/hrm-api/src/Mightyfin.Erp.Hrm.Tests/'
shared={}
def shared_file(path):
 b=(root/path).read_bytes();shared[path]=hashlib.sha256(b).hexdigest();return b.decode('utf-8-sig')
for path in [api+'OnPremMobileOnlyHost.cs',domain+'Entities/LocalMobileSession.cs',app+'Time/MobileAttendance.cs']:
 write(path,shared_file(path))
boundary=shared_file(api+'OnPremMobileApi.cs')
boundary=replace(boundary,'record OnPremMobileCompany(Guid Id, string Tenant, bool Searchable);','record OnPremMobileCompany(Guid Id, string Tenant, bool Searchable, string TimeZoneId);')
boundary=replace(boundary,'x.GetValue<bool>("Searchable"))).ToArray();','x.GetValue<bool>("Searchable"), x["TimeZoneId"] ?? "")).ToArray();')
boundary=replace(boundary,'var proxy=c["ERP:OnPremMobile:TrustedProxyAddress"];','foreach(var row in rows)TimeZoneInfo.FindSystemTimeZoneById(row.TimeZoneId);\n        var proxy=c["ERP:OnPremMobile:TrustedProxyAddress"];')
boundary=replace(boundary,'u.CredentialMode != LocalCredentialModes.Local || !LocalCredentialModes.IsValid(u)','string.IsNullOrWhiteSpace(u.Email) || string.IsNullOrWhiteSpace(u.NormalizedEmail) || string.IsNullOrWhiteSpace(u.PasswordHash)')
boundary=replace(boundary,'x.CredentialMode == LocalCredentialModes.Local\n            && x.NormalizedEmail == identifier','x.NormalizedEmail == identifier')
for old,new in [('c.TimeZoneId','CompanyZone(h,c.Id)'),('grant.Company.TimeZoneId','CompanyZone(h,grant.Company.Id)'),('g.Company.TimeZoneId','CompanyZone(h,g.Company.Id)')]:
 if old not in boundary:raise RuntimeError('Unsupported timezone adapter shape')
 boundary=boundary.replace(old,new)
boundary=replace(boundary,'private static DateTimeOffset Now(HttpContext h)', 'private static string CompanyZone(HttpContext h,Guid id)=>Options(h).Companies.Single(x=>x.Id==id).TimeZoneId;\n    private static DateTimeOffset Now(HttpContext h)')
write(api+'OnPremMobileApi.cs',boundary)

db=read(infra+'Data/HrmDbContext.cs')
db=replace(db,'public DbSet<LocalSession> LocalSessions => Set<LocalSession>();','public DbSet<LocalSession> LocalSessions => Set<LocalSession>();\n    public DbSet<LocalMobileSession> LocalMobileSessions => Set<LocalMobileSession>();')
mapping='''        ConfigureEntity<LocalMobileSession>(modelBuilder, "local_mobile_sessions", e =>
        {
            e.HasIndex(x=>x.AccessHash).IsUnique();e.HasIndex(x=>x.RefreshHash).IsUnique();
            e.Property(x=>x.RefreshHash).IsConcurrencyToken();e.Property(x=>x.RevokedAt).IsConcurrencyToken();
            e.HasIndex(x=>new{x.TenantId,x.LocalUserId,x.ExpiresAt});
            // Existing primary keys avoid introducing indexes/constraints on old business tables.
            e.HasOne<LocalUser>().WithMany().HasForeignKey(x=>x.LocalUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LegalEntity>().WithMany().HasForeignKey(x=>x.LegalEntityId).OnDelete(DeleteBehavior.Restrict);
        });
'''
db=replace(db,'        ConfigureEntity<LocalCredentialLink>',mapping+'        ConfigureEntity<LocalCredentialLink>')
write(infra+'Data/HrmDbContext.cs',db)

modern_time=shared_file(app+'Time/TimeService.cs')
punch=modern_time[modern_time.index('    public async Task<PunchResultDto> PunchMobileAsync'):modern_time.index('    private async Task<PunchResultDto> GetOrCreatePunchAsync')]
time=read(app+'Time/TimeService.cs')
time=replace(time,'IUnitOfWork? unitOfWork = null) : ITimeService','IUnitOfWork? unitOfWork = null, IMobileAttendanceEmployeeAuthority? mobileAuthority = null) : ITimeService, IMobileAttendanceService')
time=replace(time,'    private async Task<PunchResultDto> GetOrCreatePunchAsync',punch+'    private async Task<PunchResultDto> GetOrCreatePunchAsync')
write(app+'Time/TimeService.cs',time)
modern_repo=shared_file(infra+'Repositories.cs')
open_query=modern_repo[modern_repo.index('    public Task<List<AttendanceRecord>> OpenMobilePunchesAsync'):modern_repo.index('    public async Task<AttendanceRecord?> GetAttendanceAsync')]
repo=read(infra+'Repositories.cs')
repo=replace(repo,'class TimeRepository(HrmDbContext db) : ITimeRepository','class TimeRepository(HrmDbContext db) : ITimeRepository, IMobileAttendanceRepository')
repo=replace(repo,'    public async Task<AttendanceRecord?> GetAttendanceAsync',open_query+'    public async Task<AttendanceRecord?> GetAttendanceAsync')
write(infra+'Repositories.cs',repo)

program=read(api+'Program.cs')
program=replace(program,'builder.Services.AddHostedService<MonthlyLeaveAccrualWorker>();','if(!OnPremMobileOnlyHost.Enabled(builder.Configuration))builder.Services.AddHostedService<MonthlyLeaveAccrualWorker>();')
rate='''builder.Services.AddRateLimiter(options => {
    options.RejectionStatusCode=429;
    options.AddPolicy("onprem-mobile-read",h=>RateLimitPartition.GetFixedWindowLimiter(OnPremMobileApi.RateKey(h),_=>new FixedWindowRateLimiterOptions{PermitLimit=120,Window=TimeSpan.FromMinutes(1),QueueLimit=0,AutoReplenishment=true}));
    options.AddPolicy("onprem-mobile-login",h=>RateLimitPartition.GetFixedWindowLimiter(OnPremMobileApi.RateKey(h),_=>new FixedWindowRateLimiterOptions{PermitLimit=60,Window=TimeSpan.FromMinutes(1),QueueLimit=0,AutoReplenishment=true}));
});
'''
program='using Mightyfin.Erp.Hrm.Api;\nusing Microsoft.AspNetCore.RateLimiting;\nusing System.Threading.RateLimiting;\n'+program
program=replace(program,'var app = builder.Build();',rate+'\nOnPremMobileApi.Register(builder.Services,builder.Configuration,connStr);\nif(OnPremMobileOnlyHost.Enabled(builder.Configuration)){await OnPremMobileOnlyHost.RunAsync(builder,args);return;}\nvar app = builder.Build();')
write(api+'Program.cs',program)

http_tests=shared_file(tests+'OnPremMobileHttpTests.cs')
http_tests=replace(http_tests,',TimeZoneId="Africa/Lusaka"','')
http_tests=replace(http_tests,'["ERP:OnPremMobile:PublicOrigin"]=Origin,','["ERP:OnPremMobile:PublicOrigin"]=Origin,["ERP:OnPremMobile:Companies:0:TimeZoneId"]="Africa/Lusaka",["ERP:OnPremMobile:Companies:1:TimeZoneId"]="Africa/Lusaka",')
write(tests+'OnPremMobileHttpTests.cs',http_tests)
modern_tests=shared_file(tests+'TimeServiceTests.cs')
# Include all contiguous mobile attendance cases, stopping at the first existing unrelated test.
import re
blocks=re.findall(r'    \[Fact\]\n    public async Task MobilePunch_[\s\S]*?(?=    \[Fact\]|\Z)',modern_tests)
old_tests=read(tests+'TimeServiceTests.cs')
old_tests=replace(old_tests,'public class TimeServiceTests\n{','public class TimeServiceTests\n{\n'+''.join(blocks))
write(tests+'TimeServiceTests.cs',old_tests)
# Older API assemblies may not yet declare the friend test assembly.
if not any('InternalsVisibleTo("Mightyfin.Erp.Hrm.Tests")' in p.read_text(encoding='utf-8-sig') for p in (src/api).rglob('*.cs')):
 write(api+'ClassicMobileTestAccess.cs','[assembly:System.Runtime.CompilerServices.InternalsVisibleTo("Mightyfin.Erp.Hrm.Tests")]\n')
manifest={'baseline':BASE,'shared_source':subprocess.check_output(['git','-C',str(root),'rev-parse','HEAD'],text=True).strip(),
 'shared_inputs':shared,'changes':changes,'profile':'classic-local-mobile-only','existing_customer_checkout_changed':False,
 'model_adapters':['local-only account schema (no federated fields)','explicit company timezone, no legal entity rewrite','new session table references existing primary keys only']}
with (out/'source-manifest.json').open('x') as f:json.dump(manifest,f,indent=2)
print(json.dumps({'source':str(src),'baseline':BASE,'shared_source':manifest['shared_source'],'changed_files':len(changes)}))
