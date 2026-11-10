import { defineConfig, devices } from '@playwright/test';
import { readFileSync } from 'node:fs';
const values=Object.fromEntries(readFileSync('.env','utf8').trim().split('\n').filter(l=>l.includes('=')&&!l.startsWith('#')).map(l=>{const i=l.indexOf('=');return [l.slice(0,i),l.slice(i+1)];}));
process.env.DEMO_PASSWORD=values.DEMO_PASSWORD;
process.env.PLAYWRIGHT_BROWSERS_PATH=process.env.PLAYWRIGHT_BROWSERS_PATH||`${process.cwd()}/.tools/browsers`;
export default defineConfig({
  testDir:'tests/e2e',testMatch:'*.spec.mjs',fullyParallel:false,workers:1,retries:0,timeout:45000,
  use:{baseURL:process.env.TAXI_BASE_URL||`http://localhost:${values.APP_PORT||8080}`,trace:'retain-on-failure',screenshot:'only-on-failure'},
  projects:[{name:'desktop',use:{...devices['Desktop Chrome'],viewport:{width:1440,height:1000}}},{name:'mobile',use:{...devices['iPhone 13'],defaultBrowserType:'chromium'}}],
  reporter:[['list'],['html',{open:'never'}]]
});
