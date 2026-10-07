window.weatherDashboardViewport = (() => {
    let resizeHandler = null;
    let dotNetRef = null;
    let debounceHandle = 0;
    let lastBreakpoint = "";

    function getBreakpoint(width) {
        if (width < 576) {
            return "xs";
        }

        if (width < 768) {
            return "sm";
        }

        if (width < 992) {
            return "md";
        }

        return "lg";
    }

    function notify() {
        if (!dotNetRef) {
            return;
        }

        const width = window.innerWidth;
        const breakpoint = getBreakpoint(width);

        if (breakpoint === lastBreakpoint) {
            return;
        }

        lastBreakpoint = breakpoint;
        dotNetRef.invokeMethodAsync("OnViewportChanged", width);
    }

    return {
        register(reference) {
            this.dispose();

            dotNetRef = reference;
            resizeHandler = () => {
                window.clearTimeout(debounceHandle);
                debounceHandle = window.setTimeout(notify, 150);
            };

            window.addEventListener("resize", resizeHandler, { passive: true });
            notify();
        },
        dispose() {
            if (resizeHandler) {
                window.removeEventListener("resize", resizeHandler);
            }

            window.clearTimeout(debounceHandle);
            debounceHandle = 0;
            resizeHandler = null;
            dotNetRef = null;
            lastBreakpoint = "";
        }
    };
})();
