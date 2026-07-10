function clamp(value, min, max) {
  return Math.min(Math.max(value, min), max);
}

function getCandidatePosition(anchorX, anchorY, width, height, placement, offset) {
  switch (placement) {
    case "below":
      return { left: anchorX - (width / 2), top: anchorY + offset };
    case "left":
      return { left: anchorX - width - offset, top: anchorY - (height / 2) };
    case "right":
      return { left: anchorX + offset, top: anchorY - (height / 2) };
    case "above":
    default:
      return { left: anchorX - (width / 2), top: anchorY - height - offset };
  }
}

function getOverflow(position, width, height, hostWidth, hostHeight, gutter) {
  const leftOverflow = Math.max(gutter - position.left, 0);
  const topOverflow = Math.max(gutter - position.top, 0);
  const rightOverflow = Math.max((position.left + width + gutter) - hostWidth, 0);
  const bottomOverflow = Math.max((position.top + height + gutter) - hostHeight, 0);

  return leftOverflow + topOverflow + rightOverflow + bottomOverflow;
}

export function resolveTooltipPosition(hostElement, tooltipElement, anchorX, anchorY, preferredPlacement, offset = 8, gutter = 8) {
  if (!hostElement || !tooltipElement) {
    return JSON.stringify({
      left: anchorX,
      top: anchorY,
      placement: preferredPlacement ?? "above"
    });
  }

  const hostRect = hostElement.getBoundingClientRect();
  const tooltipRect = tooltipElement.getBoundingClientRect();
  const hostWidth = hostRect.width;
  const hostHeight = hostRect.height;
  const width = tooltipRect.width;
  const height = tooltipRect.height;

  const placements = preferredPlacement === "right" || preferredPlacement === "left"
    ? [preferredPlacement, preferredPlacement === "right" ? "left" : "right"]
    : [preferredPlacement, preferredPlacement === "above" ? "below" : "above"];

  let bestPlacement = placements[0];
  let bestPosition = getCandidatePosition(anchorX, anchorY, width, height, bestPlacement, offset);
  let bestOverflow = getOverflow(bestPosition, width, height, hostWidth, hostHeight, gutter);

  for (const placement of placements) {
    const position = getCandidatePosition(anchorX, anchorY, width, height, placement, offset);
    const overflow = getOverflow(position, width, height, hostWidth, hostHeight, gutter);

    if (overflow === 0) {
      bestPlacement = placement;
      bestPosition = position;
      bestOverflow = 0;
      break;
    }

    if (overflow < bestOverflow) {
      bestPlacement = placement;
      bestPosition = position;
      bestOverflow = overflow;
    }
  }

  const maxLeft = Math.max(hostWidth - width - gutter, gutter);
  const maxTop = Math.max(hostHeight - height - gutter, gutter);
  const left = clamp(bestPosition.left, gutter, maxLeft);
  const top = clamp(bestPosition.top, gutter, maxTop);

  return JSON.stringify({
    left,
    top,
    placement: bestPlacement
  });
}
