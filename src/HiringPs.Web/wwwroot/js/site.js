(()=>{
 const betaBanner = document.querySelector('[data-beta-banner]');
 if (betaBanner) {
  const storageKey = 'hiring.beta-banner.v1.dismissed';
  try { betaBanner.hidden = localStorage.getItem(storageKey) === 'true'; } catch { /* Storage may be unavailable. */ }
  betaBanner.querySelector('[data-beta-dismiss]')?.addEventListener('click', () => {
   betaBanner.hidden = true;
   try { localStorage.setItem(storageKey, 'true'); } catch { /* Dismissal still works for this page. */ }
   document.querySelector('.site-header .brand')?.focus();
  });
 }

 // The header uses the public or administration links supplied by the layout.
 const siteHeader = document.querySelector('.site-header');
 const mobileNavigation = window.matchMedia('(max-width:1100px)');
 const setupDropdown = (button, panel, isPublic = false) => {
  if (!button || !panel) return;
  const isOpen = () => button.getAttribute('aria-expanded') === 'true';
  const setOpen = open => {
   button.setAttribute('aria-expanded', String(open));
   if (isPublic) siteHeader.classList.toggle('menu-open', open);
   else panel.hidden = !open;
  };
  button.addEventListener('click', () => setOpen(!isOpen()));
  document.addEventListener('click', event => {
   if (isOpen() && !button.contains(event.target) && !panel.contains(event.target)) setOpen(false);
  });
  document.addEventListener('keydown', event => {
   if (event.key === 'Escape' && isOpen()) { setOpen(false); button.focus(); }
  });
  panel.querySelectorAll('a').forEach(link => link.addEventListener('click', () => setOpen(false)));
  panel.addEventListener('focusout', event => {
   if (isOpen() && event.relatedTarget && !panel.contains(event.relatedTarget) && !button.contains(event.relatedTarget)) setOpen(false);
  });
  if (isPublic) mobileNavigation.addEventListener('change', () => setOpen(false));
 };
 setupDropdown(document.querySelector('.header-menu-toggle'), document.getElementById('header-menu'), true);
 const connector=document.getElementById('source-connector'),connectorHelp=document.getElementById('connector-help');
 if(connector&&connectorHelp){const update=()=>{connectorHelp.textContent=connector.selectedOptions[0]?.dataset.help||'';};connector.addEventListener('change',update);update();}
 if(/^\/admin\/opportunities\/?$/i.test(location.pathname))document.querySelectorAll('.table-wrap table').forEach(table=>{table.classList.remove('admin-table');table.classList.add('table-nowrap');});
 if(document.body.classList.contains('admin-area'))document.querySelectorAll('.table-wrap table:not(.table-nowrap)').forEach(table=>{
  table.classList.add('admin-table');
  const headings=Array.from(table.querySelectorAll('thead th'),th=>th.textContent.trim());
  table.querySelectorAll('tbody tr').forEach(row=>Array.from(row.cells).forEach((cell,index)=>{cell.dataset.label=headings[index]||'';}));
 });
 if(document.querySelector('[data-collection-pending="true"]'))setTimeout(()=>location.reload(),5000);
 const editor=document.getElementById('edit-kind');
 if(editor){const update=()=>{for(const [selector,show]of[['[data-job-field]',editor.value==='Job'],['[data-training-field]',editor.value==='Training'],['[data-tender-field]',['Tender','ProcurementNotice'].includes(editor.value)]])document.querySelectorAll(selector).forEach(el=>{el.hidden=!show;if(!show)el.querySelectorAll('input').forEach(x=>x.value='');});};editor.addEventListener('change',update);update();}
 const text=document.getElementById('summary-input'),counter=document.getElementById('summary-count');
 if(text&&counter){const update=()=>{const length=typeof Intl.Segmenter==='function'?[...new Intl.Segmenter(undefined,{granularity:'grapheme'}).segment(text.value)].length:Array.from(text.value).length;counter.textContent=length+' / 200';text.setCustomValidity('');};text.addEventListener('input',update);update();}
 if(window.matchMedia('(max-width:800px)').matches)document.querySelectorAll('.filter-panel details').forEach(x=>x.open=false);
})();
