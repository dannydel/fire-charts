export function measure(hostElement, tooltipElement) {
  if (!hostElement || !tooltipElement) {
    return null;
  }

  const hostRect = hostElement.getBoundingClientRect();
  const tooltipRect = tooltipElement.getBoundingClientRect();

  return {
    hostWidth: hostRect.width,
    hostHeight: hostRect.height,
    tooltipWidth: tooltipRect.width,
    tooltipHeight: tooltipRect.height
  };
}
