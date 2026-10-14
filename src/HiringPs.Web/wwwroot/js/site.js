(()=>{
 const connector=document.getElementById('source-connector'),connectorHelp=document.getElementById('connector-help');
 if(connector&&connectorHelp){const update=()=>{connectorHelp.textContent=connector.selectedOptions[0]?.dataset.help||'';};connector.addEventListener('change',update);update();}
 if(/^\/admin\/opportunities\/?$/i.test(location.pathname))document.querySelectorAll('.table-wrap table').forEach(table=>{table.classList.remove('admin-table');table.classList.add('table-nowrap');});
 if(document.querySelector('.admin-nav'))document.querySelectorAll('.table-wrap table:not(.table-nowrap)').forEach(table=>{
  table.classList.add('admin-table');
  const headings=Array.from(table.querySelectorAll('thead th'),th=>th.textContent.trim());
  table.querySelectorAll('tbody tr').forEach(row=>Array.from(row.cells).forEach((cell,index)=>{cell.dataset.label=headings[index]||'';}));
 });
 if(document.querySelector('[data-collection-pending="true"]'))setTimeout(()=>location.reload(),5000);
 const kind=document.getElementById('kind-filter'),fields=document.getElementById('employment-filters');
 if(kind&&fields){kind.addEventListener('change',()=>{fields.hidden=kind.value!=='Job';if(fields.hidden)fields.querySelectorAll('select').forEach(x=>x.value='');});}
 const editor=document.getElementById('edit-kind');
 if(editor){const update=()=>{for(const [selector,show]of[['[data-job-field]',editor.value==='Job'],['[data-training-field]',editor.value==='Training'],['[data-tender-field]',['Tender','ProcurementNotice'].includes(editor.value)]])document.querySelectorAll(selector).forEach(el=>{el.hidden=!show;if(!show)el.querySelectorAll('input').forEach(x=>x.value='');});};editor.addEventListener('change',update);update();}
 const text=document.getElementById('summary-input'),counter=document.getElementById('summary-count');
 if(text&&counter){const update=()=>{const length=typeof Intl.Segmenter==='function'?[...new Intl.Segmenter(undefined,{granularity:'grapheme'}).segment(text.value)].length:Array.from(text.value).length;counter.textContent=length+' / 200';text.setCustomValidity('');};text.addEventListener('input',update);update();}
 if(window.matchMedia('(max-width:800px)').matches)document.querySelectorAll('.filter-panel details').forEach(x=>x.open=false);
})();
