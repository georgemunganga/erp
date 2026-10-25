import { expect, test } from '@playwright/test';

// Candidate server has no API proxy. Forward anonymous reads only to the real
// ERP; credentials and password/reset submissions are intercepted below.
test.beforeEach(async ({page}) => {
  await page.route('**/api/**',async route => {
    if (route.request().method() !== 'GET') throw new Error('Unexpected live mutation');
    const url = new URL(route.request().url());
    const response = await route.fetch({url:`https://erp.amizopower.co.zm${url.pathname}${url.search}`});
    await route.fulfill({response});
  });
});

test('ERP split login offers all email domains and preserves custom addresses', async ({page}) => {
  await page.setViewportSize({width:1440,height:900});
  let submitted: Record<string,string> | undefined;
  await page.route('**/api/hrm/auth/login',async route => {
    submitted=route.request().postDataJSON();
    await route.fulfill({status:401,contentType:'application/json',body:JSON.stringify({message:'Test credentials rejected'})});
  });
  await page.goto('/sign-in');
  await expect(page.getByRole('heading',{name:/ERP workspace/})).toBeVisible();
  await expect(page.locator('body')).not.toContainText('managed by HR');
  await expect(page).toHaveTitle(/ERP workspace/);
  const domain=page.getByRole('combobox',{name:'Email domain'});
  await expect(domain).toContainText('@amizopower.co.zm');
  await domain.click();
  for(const name of ['@amizopower.co.zm','@gmail.com','@outlook.com','@yahoo.com','Other email address']) await expect(page.getByRole('option',{name,exact:true})).toBeVisible();
  await page.getByRole('option',{name:'@gmail.com',exact:true}).click();
  await page.getByLabel('Email address',{exact:true}).fill('procurement-ui-test');
  await page.getByLabel('Password',{exact:true}).fill('NotARealPassword123');
  await page.getByRole('button',{name:'Sign in',exact:true}).click();
  await expect.poll(()=>submitted?.email).toBe('procurement-ui-test@gmail.com');
  await expect(page.getByRole('alert')).toBeVisible();
  await page.getByLabel('Email address',{exact:true}).fill('custom.user@example.org');
  await expect(domain).toContainText('Other email address');
  await page.getByRole('button',{name:'Sign in',exact:true}).click();
  await expect.poll(()=>submitted?.email).toBe('custom.user@example.org');
  await domain.click(); await page.getByRole('option',{name:'@outlook.com',exact:true}).click();
  await expect(page.getByLabel('Email address',{exact:true})).toHaveValue('custom.user');
  await page.getByRole('button',{name:'Sign in',exact:true}).click();
  await expect.poll(()=>submitted?.email).toBe('custom.user@outlook.com');
  await page.screenshot({path:(process.env.SIGNIN_TEST_OUTPUT || 'test-results/signin')+'/desktop.png'});
});

test('mobile login stays within viewport and reset reuses the chosen domain',async({page})=>{
  await page.setViewportSize({width:390,height:844});
  let resetEmail:string|undefined;
  await page.route('**/api/hrm/auth/request-password-reset',async route=>{
    resetEmail=route.request().postDataJSON().email;
    await route.fulfill({status:200,contentType:'application/json',body:'{"requested":true}'});
  });
  await page.goto('/sign-in');
  await expect(page.getByRole('button',{name:'Sign in',exact:true})).toBeVisible();
  await page.getByRole('combobox',{name:'Email domain'}).click();
  await page.getByRole('option',{name:'@yahoo.com',exact:true}).click();
  await page.getByLabel('Email address',{exact:true}).fill('procurement-ui-test');
  await page.getByRole('button',{name:'Forgot password?'}).click();
  await expect(page.getByRole('heading',{name:'Reset your password'})).toBeVisible();
  await page.getByRole('button',{name:'Email reset link'}).click();
  await expect.poll(()=>resetEmail).toBe('procurement-ui-test@yahoo.com');
  await expect(page.getByRole('status')).toContainText('If this email has an active local account');
  expect(await page.evaluate(()=>document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.screenshot({path:(process.env.SIGNIN_TEST_OUTPUT || 'test-results/signin')+'/mobile.png'});
});

test('successful sign-in returns to ERP module selection rather than HRM',async({page})=>{
  let signedIn=false;
  const user={id:'00000000-0000-0000-0000-000000000001',email:'ui-test@example.org',displayName:'UI test',roles:['erp_admin'],isActive:true,mustChangePassword:false};
  await page.route('**/api/hrm/auth/me',route=>route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({authenticated:signedIn,user:signedIn?user:null})}));
  await page.route('**/api/hrm/auth/login',async route=>{
    signedIn=true;
    await route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({authenticated:true,user})});
  });
  await page.goto('/sign-in?returnTo=%2F%0A%2Fexample.org');
  await page.getByLabel('Email address',{exact:true}).fill('ui-test@example.org');
  await page.getByLabel('Password',{exact:true}).fill('NotARealPassword123');
  await page.getByRole('button',{name:'Sign in',exact:true}).click();
  await expect(page).toHaveURL(new URL('/',process.env.PLAYWRIGHT_BASE_URL || 'http://127.0.0.1:3302').href);
  await expect(page.getByRole('heading',{name:'Choose a module'})).toBeVisible();
  await expect(page.getByRole('link').filter({hasText:'Procurement'})).toBeVisible();
  await expect(page.getByRole('link').filter({hasText:'ERP Core'})).toBeVisible();
  await page.getByRole('link').filter({hasText:'ERP Core'}).click();
  await expect(page.getByRole('heading',{name:'Organization & Settings'})).toBeVisible();
  await expect(page.locator('body')).not.toContainText('HRM access not assigned');
});


test('custom procurement identity can reach the module without an employee role',async({page})=>{
  const user={id:'00000000-0000-0000-0000-000000000002',email:'custom@example.org',displayName:'Custom buyer',roles:['custom_procurement_role'],isActive:true,mustChangePassword:false};
  await page.route('**/api/hrm/auth/me',route=>route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({authenticated:true,user})}));
  await page.goto('/');
  await expect(page.getByRole('heading',{name:'Choose a module'})).toBeVisible();
  await expect(page.getByRole('link').filter({hasText:'Procurement'})).toBeVisible();
  await expect(page.getByRole('link').filter({hasText:'ERP Core'})).toHaveCount(0);
  await page.goto('/core');
  await expect(page.getByRole('heading',{name:'Core settings access required'})).toBeVisible();
});


test('unconfigured company domain still offers the email selector',async({page})=>{
  await page.route('**/api/hrm/branding',async route=>{
    const response=await route.fetch({url:'https://erp.amizopower.co.zm/api/hrm/branding'});
    await route.fulfill({response,json:{...await response.json(),companyDomain:''}});
  });
  await page.goto('/sign-in');
  const domain=page.getByRole('combobox',{name:'Email domain'});
  await expect(domain).toBeVisible();
  await domain.click();
  await page.getByRole('option',{name:'@gmail.com',exact:true}).click();
  await expect(domain).toContainText('@gmail.com');
});
