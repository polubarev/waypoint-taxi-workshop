import test from 'node:test';
import assert from 'node:assert/strict';
import { canView,coordinateLabel,tripPayload,statusLabel,SAVED_PLACES } from '../../web/ui-logic.mjs';
test('role menu guards reject unrelated privileged views',()=>{
  assert.equal(canView(['Employee'],'admin'),false);assert.equal(canView(['Manager'],'approvals'),true);assert.equal(canView(['Manager'],'dispatch'),false);assert.equal(canView(['GisAdmin'],'zones'),true);assert.equal(canView(['SystemAdmin'],'admin'),true);assert.equal(canView([],'unknown'),false);
});
test('saved places retain canonical latitude and longitude',()=>{const body=tripPayload(SAVED_PLACES.office,SAVED_PLACES.client,'Meeting');assert.deepEqual(body.pickup,{lat:32.06,lng:34.77});assert.equal(body.dropoff.lng,34.81);assert.equal(body.note,'Meeting');assert.match(coordinateLabel(body.pickup),/32.0600/);});
test('status names match backend contract',()=>{assert.equal(statusLabel('PendingApproval'),'Pending approval');assert.equal(statusLabel('Assigned'),'Taxi assigned');});
