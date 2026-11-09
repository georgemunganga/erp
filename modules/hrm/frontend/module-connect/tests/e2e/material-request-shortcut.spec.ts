import { expect, test, type Page } from "@playwright/test";
const actor="6115aa71-334a-4e3a-97c0-24e1d7463c0f", company="01a17921-a5a8-7ac2-8912-f5c2197c82ec";
async function fixture(page:Page, allowed=true) {
 const writes:string[]=[];
 await page.route("**/api/**",route=>{
  const r=route.request(),path=new URL(r.url()).pathname;if(r.method()!=="GET")writes.push(path);
  const json=(value:unknown,status=200)=>route.fulfill({status,json:value});
  if(path==="/api/hrm/auth/capabilities")return json({mode:"local",localUsersEnabled:true,identityConfigured:false});
  if(path==="/api/hrm/auth/me")return json({authenticated:true,user:{id:actor,email:"requester@example.invalid",displayName:"Department requester",roles:["procurement_requester"],isActive:true,mustChangePassword:false}});
  if(path==="/api/hrm/me")return json({linked:false,worker:null,subject:actor});
  if(path==="/api/core/v1/workspace-context")return json({schemaVersion:1,tenantId:"fixture",principalId:actor,organisationWide:true,coreActions:[],companies:[{id:company,code:"ONE",registeredName:"Fixture company",currency:"ZMW",isDefault:true}],branches:[]});
  if(path==="/api/procurement/v1/context")return json({principalId:actor,legalEntityId:company,permissions:allowed?["procurement-access","procurement-request-read","procurement-request-create"]:["procurement-access","procurement-request-read"]});
  if(path==="/api/procurement/v1/meta")return json({capabilities:{requestDraft:true}});
  return json({code:"NotConfigured"},404);
 });
 return writes;
}
for(const width of [1440,390])test(`ERP home offers one authorised request shortcut at ${width}`,async({page})=>{
 await page.setViewportSize({width,height:1000});const writes=await fixture(page);await page.goto("/");
 await expect(page.getByRole("heading",{name:"Choose a module",exact:true})).toBeVisible();
 const shortcut=page.getByRole("region",{name:"Material and service requests"});await expect(shortcut).toBeVisible();
 await expect(shortcut.getByRole("link",{name:"Request materials or services",exact:true})).toHaveAttribute("href","/procurement/workspace/requests/new?entry=department");
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1)).toBe(true);expect(writes).toEqual([]);
});
test("ERP home hides request creation when the current grant is missing",async({page})=>{
 const writes=await fixture(page,false);await page.goto("/");await expect(page.getByRole("heading",{name:"Choose a module",exact:true})).toBeVisible();
 await expect(page.getByRole("region",{name:"Material and service requests"})).toHaveCount(0);expect(writes).toEqual([]);
});
