(() => {
    const toggle = document.querySelector('.nav-toggle');
    const navigation = document.querySelector('.nav-links');

    if (toggle && navigation) {
        toggle.addEventListener('click', () => navigation.classList.toggle('open'));
    }

    const countdowns = Array.from(
        document.querySelectorAll('.countdown[data-seconds]'),
        element => ({
            element,
            endsAt: Date.now() + Number(element.dataset.seconds) * 1000
        })
    );

    if (countdowns.length === 0) return;

    function updateCountdowns() {
        const now = Date.now();
        for (const { element, endsAt } of countdowns) {
            const remaining = Math.max(0, Math.ceil((endsAt - now) / 1000));
            const days = Math.floor(remaining / 86400);
            const hours = Math.floor(remaining / 3600) % 24;
            const minutes = Math.floor(remaining / 60) % 60;
            const seconds = remaining % 60;
            const parts = [days, hours, minutes, seconds].map(
                value => String(value).padStart(2, '0')
            );

            element.textContent = `${parts[0]}d : ${parts[1]}h : ${parts[2]}m : ${parts[3]}s`;
            element.classList.toggle('warning', remaining < 300);
        }
    }

    updateCountdowns();
    setInterval(updateCountdowns, 1000);
})();
