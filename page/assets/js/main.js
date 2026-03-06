/* ============================================================
   VIDO — GitHub Pages JavaScript
   Vanilla JS — no frameworks, no dependencies
   ============================================================ */

(function () {
  'use strict';

  // --- Header scroll effect ---
  const header = document.querySelector('.site-header');
  if (header) {
    let ticking = false;
    window.addEventListener('scroll', function () {
      if (!ticking) {
        window.requestAnimationFrame(function () {
          header.classList.toggle('scrolled', window.scrollY > 10);
          ticking = false;
        });
        ticking = true;
      }
    });
  }

  // --- Mobile navigation toggle ---
  const mobileToggle = document.querySelector('.mobile-toggle');
  const headerNav = document.querySelector('.header-nav');
  if (mobileToggle && headerNav) {
    mobileToggle.addEventListener('click', function () {
      const isOpen = headerNav.classList.toggle('open');
      mobileToggle.setAttribute('aria-expanded', isOpen);
    });

    // Close on outside click
    document.addEventListener('click', function (e) {
      if (!mobileToggle.contains(e.target) && !headerNav.contains(e.target)) {
        headerNav.classList.remove('open');
        mobileToggle.setAttribute('aria-expanded', 'false');
      }
    });

    // Close on Escape
    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape' && headerNav.classList.contains('open')) {
        headerNav.classList.remove('open');
        mobileToggle.setAttribute('aria-expanded', 'false');
        mobileToggle.focus();
      }
    });
  }

  // --- Intersection Observer for scroll animations ---
  if ('IntersectionObserver' in window) {
    const animElements = document.querySelectorAll('.animate-in');
    if (animElements.length > 0) {
      const observer = new IntersectionObserver(
        function (entries) {
          entries.forEach(function (entry) {
            if (entry.isIntersecting) {
              entry.target.style.animationPlayState = 'running';
              observer.unobserve(entry.target);
            }
          });
        },
        { threshold: 0.1, rootMargin: '0px 0px -40px 0px' }
      );

      animElements.forEach(function (el) {
        el.style.animationPlayState = 'paused';
        observer.observe(el);
      });
    }
  }

  // --- Smooth scroll for anchor links ---
  document.querySelectorAll('a[href^="#"]').forEach(function (anchor) {
    anchor.addEventListener('click', function (e) {
      var targetId = this.getAttribute('href');
      if (targetId === '#') return;
      var target = document.querySelector(targetId);
      if (target) {
        e.preventDefault();
        var headerOffset = header ? header.offsetHeight + 16 : 80;
        var top = target.getBoundingClientRect().top + window.pageYOffset - headerOffset;
        window.scrollTo({ top: top, behavior: 'smooth' });
      }
    });
  });

  // --- Active sidebar link tracking for guide pages ---
  var sidebarLinks = document.querySelectorAll('.guide-sidebar nav a');
  if (sidebarLinks.length > 0) {
    var currentPage = window.location.pathname.split('/').pop() || 'index.html';
    sidebarLinks.forEach(function (link) {
      var href = link.getAttribute('href').split('/').pop();
      if (href === currentPage) {
        link.classList.add('active');
      }
    });
  }

  // --- Set active header nav link ---
  var navLinks = document.querySelectorAll('.header-nav a:not(.header-cta)');
  if (navLinks.length > 0) {
    var path = window.location.pathname;
    navLinks.forEach(function (link) {
      var href = link.getAttribute('href');
      if (href && path.includes(href.replace(/^\.\.\//, '').replace(/^\.\//, ''))) {
        link.classList.add('active');
      }
    });
  }
})();
