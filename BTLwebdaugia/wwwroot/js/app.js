(() => {
    const toggle = document.querySelector('.nav-toggle');
    const navigation = document.querySelector('.nav-links');
    if (toggle && navigation) {
        toggle.addEventListener('click', () => navigation.classList.toggle('open'));
    }

    // WeakMap releases removed DOM nodes after AJAX replaces a countdown.
    const endTimes = new WeakMap();
    const warningSeconds = 300;

    function updateCountdowns() {
        const now = Date.now();
        document.querySelectorAll('.countdown[data-seconds]').forEach(element => {
            if (!endTimes.has(element)) {
                const seconds = Number(element.dataset.seconds);
                if (!Number.isFinite(seconds)) return;
                endTimes.set(element, now + seconds * 1000);
            }
            const remaining = Math.max(0, Math.ceil((endTimes.get(element) - now) / 1000));
            const parts = [Math.floor(remaining / 86400), Math.floor(remaining / 3600) % 24,
                Math.floor(remaining / 60) % 60, remaining % 60]
                .map(value => String(value).padStart(2, '0'));
            element.textContent = `${parts[0]}d : ${parts[1]}h : ${parts[2]}m : ${parts[3]}s`;
            element.classList.toggle('warning', remaining < warningSeconds);
        });
    }

    updateCountdowns();
    setInterval(updateCountdowns, 1000);
})();
