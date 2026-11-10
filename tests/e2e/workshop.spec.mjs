import { test, expect } from '@playwright/test';
test.beforeEach(async({page})=>{await page.route('https://tile.openstreetmap.org/**',route=>route.abort());});
async function signIn(page,username='employee') {
  await page.goto('/');await page.locator('#username').fill(username);await page.locator('#password').fill(process.env.DEMO_PASSWORD);await page.getByRole('button',{name:'Sign in'}).click();await expect(page.locator('#app-view')).toBeVisible();
}
async function token(request,name){const response=await request.post('/auth/login',{data:{username:name,password:process.env.DEMO_PASSWORD}});expect(response.ok()).toBeTruthy();return (await response.json()).accessToken;}
test('employee plans a trip, manager approves, dispatcher assigns and map updates',async({page,request})=>{
  const errors=[];page.on('pageerror',error=>errors.push(error.message));await signIn(page);
  await page.locator('#pickup-place').selectOption('office');await page.locator('#dropoff-place').selectOption('client');await page.locator('#trip-note').fill('Browser acceptance');
  await page.getByRole('button',{name:'Check route'}).click();await expect(page.locator('#book-trip')).toBeEnabled();await expect(page.locator('#distance')).toContainText('km');
  const created=page.waitForResponse(r=>r.url().endsWith('/booking/bookings')&&r.request().method()==='POST');await page.locator('#book-trip').click();const booking=await (await created).json();
  const row=page.locator(`tr[data-booking-id="${booking.id}"]`);await expect(row).toContainText('Pending approval');
  const manager=await token(request,'manager');const approved=await request.post(`/booking/bookings/${booking.id}/approve`,{headers:{Authorization:`Bearer ${manager}`}});expect(approved.ok()).toBeTruthy();
  const dispatcher=await token(request,'dispatcher');
  await expect.poll(async()=>{const r=await request.get('/gis/trips',{headers:{Authorization:`Bearer ${dispatcher}`}});return (await r.json()).features.some(f=>f.properties.bookingId===booking.id);}).toBe(true);
  const assigned=await request.post(`/booking/dispatch/${booking.id}/assign`,{data:{taxiId:'taxi-01'},headers:{Authorization:`Bearer ${dispatcher}`}});expect(assigned.ok()).toBeTruthy();
  await page.locator('#refresh-bookings').click();await expect(row).toContainText('Taxi assigned');
  const employee=await token(request,'employee');await request.post(`/booking/bookings/${booking.id}/cancel`,{headers:{Authorization:`Bearer ${employee}`}});
  expect(errors).toEqual([]);
});
test('role menus and team approval screen',async({page})=>{await signIn(page,'manager');await expect(page.locator('[data-view="approvals"]')).toBeVisible();await expect(page.locator('[data-view="admin"]')).toBeHidden();await expect(page.locator('[data-view="dispatch"]')).toBeHidden();await expect(page.locator('#table-title')).toHaveText('Team requests');});
test('GIS admin can draw a polygon and save zone details',async({page,request,isMobile})=>{
  await signIn(page,'gisadmin');await expect(page.locator('#zone-editor')).toBeVisible();await expect(page.locator('.leaflet-draw-draw-polygon')).toBeVisible();
  if(isMobile)await page.locator('.leaflet-draw-draw-polygon').tap();else await page.locator('.leaflet-draw-draw-polygon').click();const map=page.locator('#map');const box=await map.boundingBox();
  for(const [x,y] of [[.35,.55],[.8,.55],[.8,.85],[.35,.85]]) {
    if(isMobile)await page.touchscreen.tap(box.x+box.width*x,box.y+box.height*y);else await page.mouse.click(box.x+box.width*x,box.y+box.height*y);
    // Leaflet.Draw suppresses new vertices for 50ms after mouseup.
    await page.waitForTimeout(100);
  }
  if(isMobile)await page.getByRole('link',{name:'Finish',exact:true}).tap();else await page.getByRole('link',{name:'Finish',exact:true}).click();
  await expect(page.locator('#zone-delete')).toBeDisabled();
  await expect(page.locator('#zone-name')).toHaveValue('');
  await page.locator('#zone-name').fill('Browser drawn zone');await page.locator('#zone-active').uncheck();
  const saved=page.waitForResponse(r=>r.url().endsWith('/gis/zones')&&r.request().method()==='POST');await page.getByRole('button',{name:'Save zone'}).click();const response=await saved;expect(response.status()).toBe(201);
  const zone=await response.json();const gis=await token(request,'gisadmin');const zones=await request.get('/gis/zones',{headers:{Authorization:`Bearer ${gis}`}});expect((await zones.json()).features.find(f=>f.id==='10000000-0000-0000-0000-000000000001').properties.active).toBe(true);await request.delete(`/gis/zones/${zone.id}`,{headers:{Authorization:`Bearer ${gis}`}});
});
test('system admin sees users and all role controls',async({page})=>{await signIn(page,'admin');await expect(page.locator('#admin-section')).toBeVisible();await expect(page.locator('.user-row')).toHaveCount(7);await expect(page.locator('.role-options').first().getByRole('checkbox')).toHaveCount(5);});
test('layout stays within the viewport and keyboard reaches login',async({page},testInfo)=>{
  await page.goto('/');await expect(page.getByRole('heading',{name:'Good trips start here.'})).toBeVisible();await page.locator('#username').focus();await page.keyboard.press('Tab');await expect(page.locator('#password')).toBeFocused();await signIn(page);
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth)).toBe(true);
  await page.screenshot({path:`artifacts/${testInfo.project.name}-planner.png`,fullPage:true});
});
