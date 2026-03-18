export function observeElementSize(element, dotNetRef) {
  if (!element) {
    throw new Error("A host element is required.");
  }

  const notify = (width) => {
    if (typeof width === "number" && Number.isFinite(width)) {
      dotNetRef.invokeMethodAsync("OnContainerWidthChanged", width);
    }
  };

  notify(element.getBoundingClientRect().width);

  const observer = new ResizeObserver((entries) => {
    for (const entry of entries) {
      notify(entry.contentRect.width);
    }
  });

  observer.observe(element);

  return {
    dispose() {
      observer.disconnect();
      dotNetRef.dispose();
    }
  };
}
