export function observeMobileBreakpoint(element, dotNetRef, breakpoint) {
  const mediaQuery = window.matchMedia(`(max-width: ${breakpoint}px)`);

  const notify = () => {
    dotNetRef.invokeMethodAsync("OnViewportChanged", mediaQuery.matches);
  };

  notify();

  const handler = () => notify();
  mediaQuery.addEventListener("change", handler);

  return {
    dispose() {
      mediaQuery.removeEventListener("change", handler);
    }
  };
}
