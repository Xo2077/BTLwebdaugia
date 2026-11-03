
/* Shared visual behavior, progressive enhancement. */
(()=>{
 const logos = document.querySelector('.logos');
 if (logos && !logos.querySelector('.logo-track')) {
     const partners = ['ATELIER', 'HERITAGE', 'COLLECTIVE', 'OBJECTS', 'ARCHIVE'];
     const marks = Array.from(logos.children);
     const track = document.createElement('div');
     track.className = 'logo-track';

     marks.forEach((mark, index) => {
         const partner = partners[index];
         mark.dataset.partner = partner;
         mark.setAttribute('role', 'img');
         mark.setAttribute('aria-label', partner);
         track.append(mark);
     });

     marks.forEach(mark => {
         const clone = mark.cloneNode(true);
         clone.setAttribute('aria-hidden', 'true');
         track.append(clone);
     });

     logos.append(track);
 }
 const blocks=document.querySelectorAll('.panel,.metric,.auction-section,.product-card,.table-shell');
 if('IntersectionObserver' in window){const obs=new IntersectionObserver(entries=>entries.forEach(entry=>{if(entry.isIntersecting){entry.target.classList.add('in-view');obs.unobserve(entry.target)}}),{threshold:.06});blocks.forEach(el=>{el.classList.add('reveal');obs.observe(el)});} 
 const top=document.createElement('button');top.type='button';top.className='back-top';top.textContent='↑ Top';top.setAttribute('aria-label','Back to top');top.addEventListener('click',()=>window.scrollTo({top:0,behavior:'smooth'}));document.body.append(top);
 window.addEventListener('scroll',()=>top.classList.toggle('show',window.scrollY>600),{passive:true});
 const menu=document.querySelector('.nav-toggle');if(menu){menu.setAttribute('aria-expanded','false');menu.addEventListener('click',()=>menu.setAttribute('aria-expanded',String(document.querySelector('.nav-links')?.classList.contains('open'))));}
})();
