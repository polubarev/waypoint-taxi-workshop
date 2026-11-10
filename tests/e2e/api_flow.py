#!/usr/bin/env python3
"""Acceptance checks against the real localhost gateway, services, databases and broker."""
import concurrent.futures
import json
import os
from pathlib import Path
import time
import unittest
import urllib.error
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[2]
values = dict(line.split('=', 1) for line in (ROOT / '.env').read_text().splitlines() if '=' in line and not line.startswith('#'))
BASE = os.environ.get('TAXI_BASE_URL', 'http://localhost:' + values.get('APP_PORT', '8080'))


def call(path, method='GET', body=None, token=None):
    headers = {'Content-Type': 'application/json'}
    if token:
        headers['Authorization'] = 'Bearer ' + token
    data = None if body is None else json.dumps(body).encode()
    request = urllib.request.Request(BASE + path, data, headers, method=method)
    try:
        with urllib.request.urlopen(request, timeout=10) as response:
            raw = response.read()
            return response.status, json.loads(raw) if raw else None
    except urllib.error.HTTPError as error:
        raw = error.read()
        error.close()
        try:
            result = json.loads(raw) if raw else None
        except json.JSONDecodeError:
            result = {'error': raw.decode()[:200]}
        return error.code, result


def login(name):
    status, body = call('/auth/login', 'POST', {'username': name, 'password': values['DEMO_PASSWORD']})
    if status != 200:
        raise AssertionError(f'Login failed for {name}: HTTP {status}')
    return body


def wait_for(predicate, description):
    deadline = time.monotonic() + 20
    while time.monotonic() < deadline:
        value = predicate()
        if value:
            return value
        time.sleep(.25)
    raise AssertionError('Timed out: ' + description)


class Acceptance(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tokens = {name: login(name)['accessToken'] for name in ['employee', 'employee-north', 'manager', 'manager-north', 'dispatcher', 'gisadmin', 'admin']}

    def api(self, who, path, method='GET', body=None):
        return call(path, method, body, self.tokens[who])

    def create(self, who='employee'):
        status, trip = self.api(who, '/booking/bookings', 'POST', {'pickup': {'lat':32.06,'lng':34.77}, 'dropoff':{'lat':32.09,'lng':34.81}, 'note':'Acceptance ' + str(uuid.uuid4())[:8]})
        self.assertEqual(201, status, trip)
        self.assertEqual('PendingApproval', trip['status'])
        return trip

    def live(self, booking_id):
        status, collection = self.api('dispatcher', '/gis/trips')
        self.assertEqual(200, status)
        return [f for f in collection['features'] if f['properties']['bookingId'] == booking_id]

    def test_01_unauthenticated_and_forged_requests_fail(self):
        for path in ['/gis/zones', '/gis/trips', '/booking/bookings', '/auth/me']:
            self.assertEqual(401, call(path)[0], path)
        self.assertEqual(401, call('/gis/zones', token='forged.token.value')[0])
        self.assertEqual(401, call('/auth/login','POST',{'username':'employee','password':'incorrect'})[0])

    def test_02_request_approve_assign_complete_and_map(self):
        trip = self.create()
        self.assertEqual(200, self.api('manager',f"/booking/bookings/{trip['id']}/approve",'POST')[0])
        features = wait_for(lambda:self.live(trip['id']), 'approved trip reaches GIS')
        self.assertEqual([34.77,32.06], features[0]['geometry']['coordinates'][0])
        self.assertEqual('LineString',features[0]['geometry']['type'])
        status, dispatch = self.api('dispatcher','/booking/dispatch/approved')
        self.assertEqual(200,status)
        self.assertIn(trip['id'],[b['id'] for b in dispatch])
        status, assigned = self.api('dispatcher',f"/booking/dispatch/{trip['id']}/assign",'POST',{'taxiId':'taxi-01'})
        self.assertEqual(200,status,assigned)
        self.assertEqual('Assigned',assigned['status'])
        status, completed = self.api('dispatcher',f"/booking/dispatch/{trip['id']}/complete",'POST')
        self.assertEqual(200,status,completed)
        self.assertEqual('Completed',completed['status'])
        wait_for(lambda:not self.live(trip['id']), 'completed trip removed from GIS')
        status,audit=self.api('employee',f"/booking/bookings/{trip['id']}/audit")
        self.assertEqual(['Created','Approved','Assigned','Completed'],[r['action'] for r in audit])
        self.assertEqual(200,status)

    def test_03_approval_is_scoped_to_direct_manager(self):
        trip=self.create()
        self.assertEqual(403,self.api('manager-north',f"/booking/bookings/{trip['id']}/approve",'POST')[0])
        self.assertEqual(403,self.api('employee',f"/booking/bookings/{trip['id']}/approve",'POST')[0])
        self.assertEqual(200,self.api('manager',f"/booking/bookings/{trip['id']}/reject",'POST')[0])
        self.assertEqual(409,self.api('dispatcher',f"/booking/dispatch/{trip['id']}/assign",'POST',{'taxiId':'taxi-01'})[0])
        self.assertEqual(403,self.api('employee-north',f"/booking/bookings/{trip['id']}/audit")[0])
        _,mine=self.api('employee-north','/booking/bookings')
        self.assertNotIn(trip['id'],[b['id'] for b in mine])

    def test_04_zone_and_dispatch_permissions(self):
        self.assertEqual(403,self.api('employee','/gis/zones','POST',self.zone())[0])
        self.assertEqual(403,self.api('manager','/booking/dispatch/approved')[0])
        self.assertEqual(403,self.api('employee','/auth/admin/users')[0])
        self.assertEqual(403,self.api('employee','/gis/trips')[0])
        self.assertEqual(403,self.api('employee','/booking/team/bookings')[0])

    def test_05_real_postgis_geometry_and_metric_distance(self):
        route={'pickup':{'lat':32.06,'lng':34.77},'dropoff':{'lat':32.09,'lng':34.81}}
        status,result=self.api('employee','/gis/validate','POST',route)
        self.assertEqual(200,status)
        self.assertTrue(result['allowed'])
        self.assertTrue(result['inZone'])
        self.assertFalse(result['blocked'])
        self.assertEqual('central',result['regionId'])
        self.assertGreater(result['distanceKm'],4.8)
        self.assertLess(result['distanceKm'],5.2)
        _,same=self.api('employee','/gis/validate','POST',{'pickup':route['pickup'],'dropoff':route['pickup']})
        self.assertEqual(0,same['distanceKm'])
        for point in [{'lat':32.08,'lng':34.79},{'lat':31.5,'lng':34.2}]:
            _,invalid=self.api('employee','/gis/validate','POST',{'pickup':point,'dropoff':route['dropoff']})
            self.assertFalse(invalid['allowed'])
        self.assertEqual(422,self.api('employee','/booking/bookings','POST',{'pickup':{'lat':32.08,'lng':34.79},'dropoff':route['dropoff']})[0])

    @staticmethod
    def zone():
        return {'name':'Acceptance zone','type':'Service','active':False,'regionId':'central','geometry':{'type':'Polygon','coordinates':[[[34.76,32.03],[34.77,32.03],[34.77,32.04],[34.76,32.04],[34.76,32.03]]]}}

    def test_06_zone_crud_and_invalid_polygon(self):
        request=self.zone()
        status,created=self.api('gisadmin','/gis/zones','POST',request)
        self.assertEqual(201,status,created)
        zone_id=created['id']
        try:
            request['name']='Acceptance updated'
            self.assertEqual(200,self.api('gisadmin','/gis/zones/'+zone_id,'PUT',request)[0])
            _,collection=self.api('employee','/gis/zones')
            feature=next(f for f in collection['features'] if f['id']==zone_id)
            self.assertEqual('Acceptance updated',feature['properties']['name'])
            self.assertEqual([34.76,32.03],feature['geometry']['coordinates'][0][0])
            bad=self.zone();bad['geometry']['coordinates']=[[[34.76,32.03],[34.77,32.04],[34.76,32.04],[34.77,32.03],[34.76,32.03]]]
            status,problem=self.api('gisadmin','/gis/zones','POST',bad)
            self.assertEqual(400,status,problem)
            self.assertIn('Invalid polygon',problem['error'])
        finally:
            self.assertEqual(204,self.api('gisadmin','/gis/zones/'+zone_id,'DELETE')[0])

    def test_07_concurrent_approvals_have_one_winner(self):
        trip=self.create()
        with concurrent.futures.ThreadPoolExecutor(2) as pool:
            results=list(pool.map(lambda _:self.api('manager',f"/booking/bookings/{trip['id']}/approve",'POST')[0],range(2)))
        self.assertEqual([200,409],sorted(results))
        wait_for(lambda:self.live(trip['id']),'trip after concurrent approvals')
        self.assertEqual(200,self.api('employee',f"/booking/bookings/{trip['id']}/cancel",'POST')[0])
        wait_for(lambda:not self.live(trip['id']),'cancel reaches map')

    def test_08_refresh_rotates_and_cannot_be_replayed(self):
        session=login('employee')
        status,rotated=call('/auth/refresh','POST',{'refreshToken':session['refreshToken']})
        self.assertEqual(200,status)
        self.assertNotEqual(session['refreshToken'],rotated['refreshToken'])
        self.assertEqual(401,call('/auth/refresh','POST',{'refreshToken':session['refreshToken']})[0])
        self.assertEqual(204,call('/auth/logout','POST',{'refreshToken':rotated['refreshToken']},rotated['accessToken'])[0])
        self.assertEqual(401,call('/auth/refresh','POST',{'refreshToken':rotated['refreshToken']})[0])

    def test_09_role_event_revokes_cached_permission(self):
        _,me=self.api('manager','/auth/me')
        manager=me['id']
        self.assertEqual(200,self.api('manager','/booking/team/bookings')[0])
        try:
            self.assertEqual(204,self.api('admin',f'/auth/admin/users/{manager}/roles','PUT',{'roles':['Employee']})[0])
            wait_for(lambda:self.api('manager','/booking/team/bookings')[0]==403,'UserRoleChanged invalidates cached permission')
        finally:
            self.assertEqual(204,self.api('admin',f'/auth/admin/users/{manager}/roles','PUT',{'roles':['Manager']})[0])
            wait_for(lambda:self.api('manager','/booking/team/bookings')[0]==200,'manager permission restored')

    def test_11_malformed_contracts_return_400(self):
        self.assertEqual(400,call('/auth/login','POST',{})[0])
        self.assertEqual(400,self.api('employee','/gis/validate','POST',{'pickup':None,'dropoff':{'lat':32,'lng':34}})[0])
        self.assertEqual(400,self.api('employee','/booking/bookings','POST',{})[0])
        bad=self.zone();bad['geometry']['coordinates'][0][0]=['wrong',32]
        self.assertEqual(400,self.api('gisadmin','/gis/zones','POST',bad)[0])

    def test_10_seed_statuses_and_bounded_input(self):
        _,bookings=self.api('employee','/booking/bookings')
        self.assertTrue({'PendingApproval','Approved','Rejected','Assigned','Completed','Cancelled'} <= {b['status'] for b in bookings})
        self.assertEqual(400,self.api('employee','/gis/validate','POST',{'pickup':{'lat':95,'lng':34},'dropoff':{'lat':32,'lng':34}})[0])
        self.assertEqual(400,self.api('admin','/auth/admin/users','POST',{'username':'invalid','displayName':'Invalid','password':'short','teamId':'central','managerId':None,'roles':['Unknown']})[0])

if __name__=='__main__':
    unittest.main(verbosity=2)
