
/* Shared visual behavior, progressive enhancement. */
(()=>{
 const logos=document.querySelector('.logos');
 if(logos&&!logos.querySelector('.logo-track')){
  const children=[...logos.children];
  const track=document.createElement('div');track.className='logo-track';
  const partners=['ATELIER','HERITAGE','COLLECTIVE','OBJECTS','ARCHIVE'];
  children.forEach((el,i)=>{el.setAttribute('data-partner',partners[i%partners.length]);el.setAttribute('aria-label',partners[i%partners.length]+' sample partner mark');el.textContent='';track.append(el)});
  children.forEach(el=>{let clone=el.cloneNode(true);clone.setAttribute('aria-hidden','true');track.append(clone)});
  logos.append(track);logos.setAttribute('aria-label','Scrolling sample partner names');
 }
 const hero=document.querySelector('.hero .image-box');if(hero){hero.textContent='';hero.setAttribute('aria-label','BidHub illustrated auction hero');}
 document.querySelectorAll('.image-box:not(.gallery-main):not(.logo-placeholder)').forEach((el,i)=>{
   if(el===hero||el.hasAttribute('data-product-image')||el.closest('.thumbs'))return;
   if(!el.textContent.trim()||/IMAGE|PHOTO|PLACEHOLDER/i.test(el.textContent)){
      el.textContent='';el.style.backgroundImage=`url('/assets/product-${i%12+1}.svg')`;el.style.backgroundPosition='center';el.style.backgroundSize='cover';
      el.setAttribute('aria-label','Illustrative sample product image');
   }
 });
 const blocks=document.querySelectorAll('.panel,.metric,.auction-section,.product-card,.table-shell');
 if('IntersectionObserver' in window){const obs=new IntersectionObserver(entries=>entries.forEach(entry=>{if(entry.isIntersecting){entry.target.classList.add('in-view');obs.unobserve(entry.target)}}),{threshold:.06});blocks.forEach(el=>{el.classList.add('reveal');obs.observe(el)});} 
 const top=document.createElement('button');top.type='button';top.className='back-top';top.textContent='↑ Top';top.setAttribute('aria-label','Back to top');top.addEventListener('click',()=>window.scrollTo({top:0,behavior:'smooth'}));document.body.append(top);
 window.addEventListener('scroll',()=>top.classList.toggle('show',window.scrollY>600),{passive:true});
 const menu=document.querySelector('.nav-toggle');if(menu){menu.setAttribute('aria-expanded','false');menu.addEventListener('click',()=>menu.setAttribute('aria-expanded',String(document.querySelector('.nav-links')?.classList.contains('open'))));}
})();

// Fixed category filtering on category.html (demo listings only)
document.addEventListener('DOMContentLoaded',()=>{const selector=document.querySelector('#category-select');if(!selector||!document.querySelector('.filter-layout'))return;const apply=()=>{const val=selector.value;const cards=[...document.querySelectorAll('.filter-layout article[data-category]')];cards.forEach(card=>card.hidden=!!val&&card.dataset.category!==val);let empty=document.querySelector('#category-empty-message');if(!empty){empty=document.createElement('p');empty.id='category-empty-message';empty.className='empty-message';document.querySelector('.filter-layout .grid')?.appendChild(empty)}empty.hidden=cards.some(c=>!c.hidden)};selector.addEventListener('change',apply);apply();});
