import { ROLES, SAVED_PLACES, canView, coordinateLabel, statusLabel, tripPayload } from './ui-logic.mjs';
const $ = id => document.getElementById(id);
const state = { session:null,view:'request',pickup:null,dropoff:null,pick:'pickup',valid:false,revision:0,map:null,zones:null,live:null,markers:{},route:null,draw:null,drawing:false,selectionBlockedUntil:0,selectedZone:null,geometry:null,taxis:[],teams:[] };
let noticeTimer;
function notify(message,error=false) { $('notice').textContent=message;$('notice').className=error?'error':'';$('notice').hidden=false;clearTimeout(noticeTimer);noticeTimer=setTimeout(()=>$('notice').hidden=true,7000); }
const run = fn => async event => { try { await fn(event); } catch(error) { notify(error.message,true); } };
async function api(path,options={},retry=true) {
  const response=await fetch(path,{...options,headers:{'Content-Type':'application/json',...(state.session?{Authorization:`Bearer ${state.session.accessToken}`} : {}),...options.headers}});
  if(response.status===401 && state.session && retry) {
    const rotated=await fetch('/auth/refresh',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({refreshToken:state.session.refreshToken})});
    if(rotated.ok) { state.session=await rotated.json();return api(path,options,false); }
    signOut();throw new Error('Your session expired. Please sign in again.');
  }
  if(!response.ok) { const problem=await response.json().catch(()=>({}));throw new Error(problem.error || `Request failed (${response.status})`); }
  return response.status===204?null:response.json();
}
const post = (path,body) => api(path,{method:'POST',...(body===undefined?{}:{body:JSON.stringify(body)})});
function element(tag,text,className) { const node=document.createElement(tag);if(text!==undefined)node.textContent=text;if(className)node.className=className;return node; }
function button(label,action) { const b=element('button',label);b.type='button';b.addEventListener('click',run(action));return b; }
$('today').textContent=new Date().toLocaleDateString('en-GB',{day:'numeric',month:'short',year:'numeric'});
$('login-form').addEventListener('submit',run(async event=>{
  event.preventDefault();const submit=event.submitter;submit.disabled=true;
  try {
    state.session=await post('/auth/login',{username:$('username').value.trim(),password:$('password').value});
    $('password').value='';$('login-view').hidden=true;$('app-view').hidden=false;$('logout').hidden=false;
    $('profile-name').textContent=state.session.user.displayName;$('initials').textContent=state.session.user.displayName.split(' ').slice(0,2).map(p=>p[0]).join('');
    document.querySelectorAll('[data-view]').forEach(b=>b.hidden=!canView(state.session.user.roles,b.dataset.view));
    setupMap();await loadZones();await loadLive();
    const roles=state.session.user.roles;
    await showView(roles.includes('Dispatcher')?'dispatch':roles.includes('Manager')?'approvals':roles.includes('GisAdmin')?'zones':roles.includes('SystemAdmin')?'admin':'request');
  } finally { submit.disabled=false; }
}));
function signOut() {
  state.session=null;$('login-view').hidden=false;$('app-view').hidden=true;$('logout').hidden=true;
  document.querySelectorAll('[data-view]').forEach(b=>b.hidden=!['request','bookings'].includes(b.dataset.view));
  if(state.map){state.map.remove();state.map=null;}state.markers={};state.route=null;state.draw=null;state.zones=null;state.live=null;
  state.pickup=null;state.dropoff=null;state.valid=false;state.geometry=null;state.selectedZone=null;
  $('pickup-place').value='';$('dropoff-place').value='';$('pickup-coords').textContent='Click the map to set pickup';$('dropoff-coords').textContent='Then click to set drop-off';$('trip-note').value='';$('book-trip').disabled=true;$('trip-summary').hidden=true;
}
$('logout').addEventListener('click',run(async()=>{try{await post('/auth/logout',{refreshToken:state.session.refreshToken});}finally{signOut();}}));
document.querySelectorAll('[data-view]').forEach(b=>b.addEventListener('click',run(()=>showView(b.dataset.view))));
const viewCopy={request:['LET’S GET YOU THERE','Where are you headed?','Choose your stops. We’ll check the route before you book.'],bookings:['EVERY JOURNEY, IN ONE PLACE','Your trips, at a glance.','Follow your requests from approval to arrival.'],approvals:['KEEP YOUR TEAM MOVING','A little approval. A long way.','Review and approve work trips for your team.'],dispatch:['READY WHEN THEY ARE','Good journeys need great coordination.','Assign a driver and keep an eye on the live map.'],zones:['A MAP THAT WORKS FOR YOUR TEAM','Give every trip a clear boundary.','Draw and manage service areas and restricted zones.'],admin:['THE RIGHT PEOPLE. THE RIGHT ACCESS.','Your team, connected.','Manage people, teams, and their roles.']};
async function showView(view) {
  if(!state.session || !canView(state.session.user.roles,view)) throw new Error('You do not have access to this view.');
  state.view=view;document.querySelectorAll('[data-view]').forEach(b=>b.classList.toggle('active',b.dataset.view===view));
  const copy=viewCopy[view];$('view-eyebrow').textContent=copy[0];$('view-title').textContent=copy[1];$('view-description').textContent=copy[2];$('view-crumb').textContent=document.querySelector(`[data-view="${view}"]`).textContent.trim();
  $('planner').hidden=!['request','dispatch','zones'].includes(view);document.querySelector('.trip-card').hidden=view!=='request';$('planner').style.gridTemplateColumns=view==='request'?'':'1fr';
  $('bookings-section').hidden=!['bookings','approvals','dispatch'].includes(view);$('zone-editor').hidden=view!=='zones';$('admin-section').hidden=view!=='admin';$('how-it-works').hidden=view!=='request';
  if(state.draw){state.map.removeControl(state.draw);state.draw=null;}
  if(view==='zones') addDrawTools();
  if(view==='admin') await loadUsers();
  if(['bookings','approvals','dispatch'].includes(view)) await loadBookings();
  if(getComputedStyle($('planner')).display!=='none') state.map.invalidateSize();
}
function setupMap() {
  if(state.map)return;
  state.map=L.map('map',{zoomControl:true}).setView([32.08,34.80],12);
  // Tiles are optional: the locally served map, zone geometry and editing work without them.
  L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png',{attribution:'&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',maxZoom:19}).addTo(state.map);
  state.zones=L.featureGroup().addTo(state.map);state.live=L.geoJSON(null,{style:{color:'#527be2',weight:3,opacity:.8}}).addTo(state.map);
  state.map.on('click',event=>{if(state.view==='request'){setPoint(state.pick,event.latlng);}});
  state.map.on(L.Draw.Event.DRAWSTART,()=>{state.drawing=true;clearZone();});
  state.map.on(L.Draw.Event.DRAWSTOP,()=>{state.drawing=false;state.selectionBlockedUntil=Date.now()+500;});
  state.map.on(L.Draw.Event.EDITSTART,()=>{state.drawing=true;});
  state.map.on(L.Draw.Event.EDITSTOP,()=>{state.drawing=false;state.selectionBlockedUntil=Date.now()+500;});
  state.map.on(L.Draw.Event.CREATED,event=>{state.zones.addLayer(event.layer);state.geometry=event.layer.toGeoJSON().geometry;state.selectedZone=null;$('zone-delete').disabled=true;notify('Polygon drawn. Add a name and save the zone.');});
  state.map.on(L.Draw.Event.EDITED,run(async event=>{
    for(const layer of event.layers.getLayers()) {
      const feature=layer.feature;
      if(!feature?.id)continue;
      await api(`/gis/zones/${feature.id}`,{method:'PUT',body:JSON.stringify({...feature.properties,geometry:layer.toGeoJSON().geometry})});
    }
    await loadZones();notify('Zone geometry updated.');
  }));
}
async function loadZones() {
  const collection=await api('/gis/zones');state.zones.clearLayers();
  L.geoJSON(collection,{style:feature=>({color:feature.properties.type==='Restricted'?'#d69c84':'#77b49c',weight:1.5,fillColor:feature.properties.type==='Restricted'?'#f3c1ad':'#a0c7b4',fillOpacity:feature.properties.active?.2:.04,dashArray:feature.properties.active?null:'4 5'}),onEachFeature:(feature,layer)=>{
    const popup=element('div');popup.append(element('strong',feature.properties.name),element('div',`${feature.properties.type} · ${feature.properties.regionId}${feature.properties.active?'':' · Inactive'}`));
    layer.on('click',event=>{
      // iOS can send a synthetic click after a draw toolbar touch has removed itself.
      if(state.drawing||Date.now()<state.selectionBlockedUntil)return;
      L.popup().setLatLng(event.latlng).setContent(popup).openOn(state.map);
      if(state.view==='zones'){state.selectedZone=feature.id;state.geometry=layer.toGeoJSON().geometry;$('zone-name').value=feature.properties.name;$('zone-type').value=feature.properties.type;$('zone-region').value=feature.properties.regionId;$('zone-active').checked=feature.properties.active;$('zone-delete').disabled=false;}
    });
  }}).getLayers().forEach(layer=>state.zones.addLayer(layer));
}
async function loadLive(){if(!state.session||!state.live||!state.session.user.roles.includes('Dispatcher'))return;const collection=await api('/gis/trips');state.live.clearLayers();state.live.addData(collection);}
function addDrawTools() {state.draw=new L.Control.Draw({draw:{polygon:{allowIntersection:false,showArea:true},polyline:false,rectangle:false,circle:false,circlemarker:false,marker:false},edit:{featureGroup:state.zones,remove:false}});state.map.addControl(state.draw);}
function setPoint(kind,point) {
  state[kind]={lat:point.lat,lng:point.lng};state.valid=false;state.revision++;$('book-trip').disabled=true;$('trip-summary').hidden=true;
  $('validation').className='validation';$('validation').querySelector('strong').textContent='Ready for a route check';$('validation').querySelector('small').textContent='Check the route before requesting approval.';
  if(state.markers[kind])state.map.removeLayer(state.markers[kind]);
  state.markers[kind]=L.marker([point.lat,point.lng],{icon:L.divIcon({className:`stop-marker ${kind==='dropoff'?'destination':''}`,html:kind==='pickup'?'A':'B',iconSize:[27,27],iconAnchor:[13,13]})}).addTo(state.map);
  $(`${kind}-coords`).textContent=coordinateLabel(point);
  if(state.route)state.map.removeLayer(state.route);
  if(state.pickup&&state.dropoff)state.route=L.polyline([[state.pickup.lat,state.pickup.lng],[state.dropoff.lat,state.dropoff.lng]],{color:'#527be2',weight:3,dashArray:'6 7'}).addTo(state.map);
  if(kind==='pickup'&&!state.dropoff)setPick('dropoff');
}
function setPick(kind){state.pick=kind;$('pick-pickup').classList.toggle('selected',kind==='pickup');$('pick-dropoff').classList.toggle('selected',kind==='dropoff');}
for(const kind of ['pickup','dropoff']) {
  $(`pick-${kind}`).addEventListener('click',()=>setPick(kind));
  $(`${kind}-place`).addEventListener('change',event=>{const place=SAVED_PLACES[event.target.value];if(place){setPoint(kind,place);state.map.panTo([place.lat,place.lng]);}});
}
$('recenter').addEventListener('click',()=>state.map.setView([32.08,34.80],12));
$('check-trip').addEventListener('click',run(async()=>{
  if(!state.pickup||!state.dropoff)throw new Error('Choose both pickup and drop-off first.');
  const revision=state.revision;const result=await post('/gis/validate',tripPayload(state.pickup,state.dropoff));if(revision!==state.revision)return;
  state.valid=result.allowed;$('book-trip').disabled=!result.allowed;$('validation').className='validation '+(result.allowed?'success':'error');
  $('validation').querySelector('strong').textContent=result.allowed?'Your route is in the service area.':'This route needs another look.';
  $('validation').querySelector('small').textContent=result.reason??'Both stops are outside restricted zones.';
  $('distance').textContent=`${result.distanceKm.toFixed(2)} km`;$('trip-summary').hidden=!result.allowed;
}));
$('book-trip').addEventListener('click',run(async()=>{
  if(!state.valid)throw new Error('Check your route first.');$('book-trip').disabled=true;
  try{const booking=await post('/booking/bookings',tripPayload(state.pickup,state.dropoff,$('trip-note').value));notify(`Trip ${booking.id.slice(0,8)} sent to your manager.`);await showView('bookings');}finally{$('book-trip').disabled=!state.valid;}
}));
async function loadBookings() {
  const view=state.view;const path=view==='approvals'?'/booking/team/bookings':view==='dispatch'?'/booking/dispatch/approved':'/booking/bookings';
  const bookings=await api(path);if(view==='dispatch')state.taxis=await api('/booking/dispatch/taxis');
  $('table-title').textContent=view==='approvals'?'Team requests':view==='dispatch'?'Dispatch queue':'Your bookings';
  $('table-description').textContent=view==='approvals'?'Only your team’s requests appear here.':view==='dispatch'?'Approved and assigned trips, ready to move.':'Every trip, from request to arrival.';
  $('booking-rows').replaceChildren();$('empty-state').hidden=bookings.length>0;
  for(const booking of bookings) {
    const tr=element('tr');tr.dataset.bookingId=booking.id;const name=element('td');name.append(element('strong',`#${booking.id.slice(0,8)}`),element('small',booking.note||'Work trip'));tr.append(name);
    const route=element('td');route.append(element('span',`${booking.pickup.lat.toFixed(3)}, ${booking.pickup.lng.toFixed(3)}`),element('small',`→ ${booking.dropoff.lat.toFixed(3)}, ${booking.dropoff.lng.toFixed(3)}`));tr.append(route,element('td',`${booking.distanceKm.toFixed(2)} km`));
    const status=element('td');status.append(element('span',statusLabel(booking.status),'badge '+booking.status));tr.append(status);const actions=element('td');const group=element('div',undefined,'row-actions');
    async function act(path,body){await post(path,body);await loadBookings();await loadLive();notify('Booking updated.');}
    if(view==='approvals'&&booking.status==='PendingApproval'){group.append(button('Approve',()=>act(`/booking/bookings/${booking.id}/approve`)),button('Reject',()=>act(`/booking/bookings/${booking.id}/reject`)));}
    if(view==='dispatch'&&booking.status==='Approved') {
      const select=element('select');select.setAttribute('aria-label',`Taxi for ${booking.id.slice(0,8)}`);state.taxis.forEach(t=>{const option=element('option',`${t.driver} · ${t.plate}`);option.value=t.id;select.append(option);});group.append(select,button('Assign taxi',()=>act(`/booking/dispatch/${booking.id}/assign`,{taxiId:select.value})));
    }
    if(view==='dispatch'&&booking.status==='Assigned')group.append(button('Complete',()=>act(`/booking/dispatch/${booking.id}/complete`)));
    if(['PendingApproval','Approved','Assigned'].includes(booking.status))group.append(button('Cancel',()=>act(`/booking/bookings/${booking.id}/cancel`)));
    group.append(button('History',async()=>{const audit=await api(`/booking/bookings/${booking.id}/audit`);notify(audit.map(row=>`${row.action} · ${new Date(row.at).toLocaleTimeString()}`).join(' → '));}));actions.append(group);tr.append(actions);$('booking-rows').append(tr);
  }
}
$('refresh-bookings').addEventListener('click',run(loadBookings));
$('zone-form').addEventListener('submit',run(async event=>{
  event.preventDefault();if(!state.geometry)throw new Error('Draw or select a polygon first.');
  const request={name:$('zone-name').value,type:$('zone-type').value,regionId:$('zone-region').value,active:$('zone-active').checked,geometry:state.geometry};
  await api('/gis/zones'+(state.selectedZone?'/'+state.selectedZone:''),{method:state.selectedZone?'PUT':'POST',body:JSON.stringify(request)});
  await loadZones();clearZone();notify('Zone saved.');
}));
function clearZone(){state.selectedZone=null;state.geometry=null;$('zone-name').value='';$('zone-delete').disabled=true;}
$('zone-new').addEventListener('click',()=>{clearZone();notify('Use the polygon tool on the map to draw a zone.');});
$('zone-delete').addEventListener('click',run(async()=>{if(!state.selectedZone)return;await api('/gis/zones/'+state.selectedZone,{method:'DELETE'});clearZone();await loadZones();notify('Zone deleted.');}));
async function loadUsers() {
  const users=await api('/auth/admin/users');state.teams=await api('/auth/admin/teams');$('new-team').replaceChildren();state.teams.forEach(team=>{const o=element('option',team.name);o.value=team.id;$('new-team').append(o);});$('user-rows').replaceChildren();
  users.forEach(user=>{
    const row=element('div',undefined,'user-row');const label=element('div');label.append(element('strong',user.displayName),element('small',`${user.username} · ${user.teamId}`));row.append(label);const choices=element('div',undefined,'role-options');
    const inputs=ROLES.map(role=>{const label=element('label');const input=element('input');input.type='checkbox';input.value=role;input.checked=user.roles.includes(role);label.append(input,document.createTextNode(role));choices.append(label);return input;});row.append(choices);
    const save=button('Save roles',async()=>{await api(`/auth/admin/users/${user.id}/roles`,{method:'PUT',body:JSON.stringify({roles:inputs.filter(i=>i.checked).map(i=>i.value)})});notify('Roles updated. The user should refresh their session for new menu access.');});save.className='secondary compact';row.append(save);$('user-rows').append(row);
  });
}
$('create-user-form').addEventListener('submit',run(async event=>{event.preventDefault();const team=state.teams.find(t=>t.id===$('new-team').value);await post('/auth/admin/users',{username:$('new-username').value,displayName:$('new-display').value,password:$('new-password').value,teamId:team.id,managerId:team.managerId,roles:[$('new-role').value]});event.target.reset();await loadUsers();notify('User created.');}));
setInterval(()=>{if(state.session)loadLive().catch(()=>{});},4000);
