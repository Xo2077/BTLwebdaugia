document.addEventListener("DOMContentLoaded", function () {

            const toggle = document.getElementById("nav-avatar-toggle");
            const menu = document.getElementById("nav-avatar-menu");

            if (!toggle || !menu) return;

            toggle.addEventListener("click", function (event) {
                event.stopPropagation();

                menu.hidden = !menu.hidden;

                toggle.setAttribute(
                    "aria-expanded",
                    String(!menu.hidden)
                );
            });

            document.addEventListener("click", function (event) {
                if (!event.target.closest(".nav-avatar-dropdown")) {
                    menu.hidden = true;
                    toggle.setAttribute("aria-expanded", "false");
                }
            });

            document.addEventListener("keydown", function (event) {
                if (event.key === "Escape") {
                    menu.hidden = true;
                    toggle.setAttribute("aria-expanded", "false");
                    toggle.focus();
                }
            });

        });
