function drawRoundedRect(context, rect, radius) {
  const safeRadius = Math.max(0, Math.min(radius, rect.width / 2, rect.height / 2));

  if (typeof context.roundRect === "function") {
    context.beginPath();
    context.roundRect(rect.x, rect.y, rect.width, rect.height, safeRadius);
    context.closePath();
    return;
  }

  context.beginPath();
  context.moveTo(rect.x + safeRadius, rect.y);
  context.lineTo(rect.x + rect.width - safeRadius, rect.y);
  context.quadraticCurveTo(rect.x + rect.width, rect.y, rect.x + rect.width, rect.y + safeRadius);
  context.lineTo(rect.x + rect.width, rect.y + rect.height - safeRadius);
  context.quadraticCurveTo(rect.x + rect.width, rect.y + rect.height, rect.x + rect.width - safeRadius, rect.y + rect.height);
  context.lineTo(rect.x + safeRadius, rect.y + rect.height);
  context.quadraticCurveTo(rect.x, rect.y + rect.height, rect.x, rect.y + rect.height - safeRadius);
  context.lineTo(rect.x, rect.y + safeRadius);
  context.quadraticCurveTo(rect.x, rect.y, rect.x + safeRadius, rect.y);
  context.closePath();
}

function drawPlaceholderCells(context, cells, radius) {
  for (const cell of cells) {
    context.save();
    drawRoundedRect(context, cell, radius);
    context.fillStyle = cell.fill;
    context.fill();
    context.strokeStyle = "rgba(137, 118, 125, 0.12)";
    context.lineWidth = 1;
    context.setLineDash([2, 2]);
    context.stroke();
    context.restore();
  }
}

function drawCells(context, cells, radius, state) {
  const selectedKey = state?.selectedCellKey ?? null;
  const hoveredKey = state?.hoveredCellKey ?? null;
  const focusedKey = state?.focusedCellKey ?? null;

  for (const cell of cells) {
    const key = getCellKey(cell);
    const isSelected = selectedKey === key;
    const isHovered = hoveredKey === key;
    const isFocused = focusedKey === key;

    context.save();
    drawRoundedRect(context, cell, radius);
    context.fillStyle = cell.fill;
    context.fill();

    if (isSelected) {
      context.shadowColor = "rgba(37, 29, 35, 0.22)";
      context.shadowBlur = 18;
      context.strokeStyle = "rgba(28, 20, 25, 0.9)";
      context.lineWidth = 1.6;
    } else if (isHovered || isFocused) {
      context.shadowColor = "rgba(37, 29, 35, 0.16)";
      context.shadowBlur = 14;
      context.strokeStyle = "rgba(53, 35, 43, 0.68)";
      context.lineWidth = 1.3;
    } else {
      context.strokeStyle = "rgba(255, 252, 248, 0.94)";
      context.lineWidth = 1.1;
    }

    context.stroke();
    context.restore();
  }
}

function drawCell(context, cell, radius, state) {
  if (!cell) {
    return;
  }

  const selectedKey = state?.selectedCellKey ?? null;
  const hoveredKey = state?.hoveredCellKey ?? null;
  const focusedKey = state?.focusedCellKey ?? null;
  const key = getCellKey(cell);
  const isSelected = selectedKey === key;
  const isHovered = hoveredKey === key;
  const isFocused = focusedKey === key;

  context.save();
  drawRoundedRect(context, cell, radius);
  context.fillStyle = cell.fill;
  context.fill();

  if (isSelected) {
    context.shadowColor = "rgba(37, 29, 35, 0.22)";
    context.shadowBlur = 18;
    context.strokeStyle = "rgba(28, 20, 25, 0.9)";
    context.lineWidth = 1.6;
  } else if (isHovered || isFocused) {
    context.shadowColor = "rgba(37, 29, 35, 0.16)";
    context.shadowBlur = 14;
    context.strokeStyle = "rgba(53, 35, 43, 0.68)";
    context.lineWidth = 1.3;
  } else {
    context.strokeStyle = "rgba(255, 252, 248, 0.94)";
    context.lineWidth = 1.1;
  }

  context.stroke();
  context.restore();
}

function hitTest(cells, x, y) {
  for (const cell of cells) {
    if (x >= cell.x && x <= cell.x + cell.width && y >= cell.y && y <= cell.y + cell.height) {
      return cell;
    }
  }

  return null;
}

function buildLookup(request) {
  const rowBounds = new Map();
  const columnBounds = new Map();
  const cellMap = new Map();

  for (const cell of request.cells || []) {
    const row = rowBounds.get(cell.rowIndex);
    if (!row) {
      rowBounds.set(cell.rowIndex, { index: cell.rowIndex, start: cell.y, end: cell.y + cell.height });
    } else {
      row.start = Math.min(row.start, cell.y);
      row.end = Math.max(row.end, cell.y + cell.height);
    }

    const column = columnBounds.get(cell.columnIndex);
    if (!column) {
      columnBounds.set(cell.columnIndex, { index: cell.columnIndex, start: cell.x, end: cell.x + cell.width });
    } else {
      column.start = Math.min(column.start, cell.x);
      column.end = Math.max(column.end, cell.x + cell.width);
    }

    cellMap.set(getCellKey(cell), cell);
  }

  return {
    rows: Array.from(rowBounds.values()).sort((left, right) => left.start - right.start),
    columns: Array.from(columnBounds.values()).sort((left, right) => left.start - right.start),
    cellMap
  };
}

function findBand(bands, coordinate) {
  let low = 0;
  let high = bands.length - 1;

  while (low <= high) {
    const mid = Math.floor((low + high) / 2);
    const band = bands[mid];

    if (coordinate < band.start) {
      high = mid - 1;
      continue;
    }

    if (coordinate > band.end) {
      low = mid + 1;
      continue;
    }

    return band;
  }

  return null;
}

function hitTestLookup(controller, x, y) {
  const row = findBand(controller.lookup?.rows || [], y);
  if (!row) {
    return null;
  }

  const column = findBand(controller.lookup?.columns || [], x);
  if (!column) {
    return null;
  }

  return controller.lookup.cellMap.get(`${row.index}:${column.index}`) || null;
}

function getCellKey(cell) {
  return `${cell.rowIndex}:${cell.columnIndex}`;
}

function getCanvasPoint(canvas, event) {
  const rect = canvas.getBoundingClientRect();
  return {
    x: event.clientX - rect.left,
    y: event.clientY - rect.top
  };
}

function drawHeatmap(canvas, request, state) {
  if (!canvas) {
    throw new Error("A canvas element is required.");
  }

  const deviceScale = window.devicePixelRatio || 1;
  const width = Math.max(request.width || 0, 1);
  const height = Math.max(request.height || 0, 1);

  canvas.width = Math.round(width * deviceScale);
  canvas.height = Math.round(height * deviceScale);
  canvas.style.width = `${width}px`;
  canvas.style.height = `${height}px`;

  const context = canvas.getContext("2d");
  context.setTransform(deviceScale, 0, 0, deviceScale, 0, 0);
  context.clearRect(0, 0, width, height);

  drawPlaceholderCells(context, request.placeholderCells || [], request.cornerRadius || 0);
  drawCells(context, request.cells || [], request.cornerRadius || 0, state);
}

function redrawCells(canvas, controller, keys) {
  if (!canvas || !controller?.request || !keys?.size) {
    return;
  }

  const context = canvas.getContext("2d");
  const request = controller.request;
  const padding = Math.max((request.cornerRadius || 0) + 24, 24);

  for (const key of keys) {
    const cell = controller.lookup?.cellMap?.get(key);
    if (!cell) {
      continue;
    }

    const clearX = Math.max(cell.x - padding, 0);
    const clearY = Math.max(cell.y - padding, 0);
    const clearWidth = Math.min(cell.width + (padding * 2), request.width - clearX);
    const clearHeight = Math.min(cell.height + (padding * 2), request.height - clearY);

    context.clearRect(clearX, clearY, clearWidth, clearHeight);
    drawCell(context, cell, request.cornerRadius || 0, controller.state);
  }
}

export function upsertHeatmap(canvas, dotNetRef, requestJson, stateJson) {
  if (!canvas) {
    throw new Error("A canvas element is required.");
  }

  const request = JSON.parse(requestJson);
  const state = stateJson ? JSON.parse(stateJson) : {};
  let controller = canvas.__fireChartsHeatmap;

  if (!controller) {
    controller = {
      lastHoverKey: null,
      request: null,
      state: {},
      lookup: null,
      onPointerMove: null,
      onPointerLeave: null,
      onClick: null
    };

    controller.onPointerMove = (event) => {
      const point = getCanvasPoint(canvas, event);
      const cell = hitTestLookup(controller, point.x, point.y);

      if (!cell) {
        if (controller.lastHoverKey !== null) {
          controller.lastHoverKey = null;
          dotNetRef.invokeMethodAsync("NotifyPointerLeave");
        }

        return;
      }

      const key = getCellKey(cell);
      if (controller.lastHoverKey === key) {
        return;
      }

      controller.lastHoverKey = key;
      dotNetRef.invokeMethodAsync("NotifyPointerMove", cell.rowIndex, cell.columnIndex);
    };

    controller.onPointerLeave = () => {
      if (controller.lastHoverKey === null) {
        return;
      }

      controller.lastHoverKey = null;
      dotNetRef.invokeMethodAsync("NotifyPointerLeave");
    };

    controller.onClick = (event) => {
      const point = getCanvasPoint(canvas, event);
      const cell = hitTestLookup(controller, point.x, point.y);
      if (!cell) {
        return;
      }

      canvas.parentElement?.focus?.();
      dotNetRef.invokeMethodAsync("NotifyClick", cell.rowIndex, cell.columnIndex);
    };

    canvas.addEventListener("pointermove", controller.onPointerMove);
    canvas.addEventListener("pointerleave", controller.onPointerLeave);
    canvas.addEventListener("click", controller.onClick);
    canvas.__fireChartsHeatmap = controller;
  }

  controller.request = request;
  controller.state = state;
  controller.lookup = buildLookup(request);
  drawHeatmap(canvas, request, state);
}

export function updateHeatmapState(canvas, stateJson) {
  const controller = canvas?.__fireChartsHeatmap;
  if (!controller) {
    return;
  }

  const previousState = controller.state || {};
  controller.state = stateJson ? JSON.parse(stateJson) : {};

  const affectedKeys = new Set();
  for (const key of [
    previousState.selectedCellKey,
    previousState.hoveredCellKey,
    previousState.focusedCellKey,
    controller.state.selectedCellKey,
    controller.state.hoveredCellKey,
    controller.state.focusedCellKey
  ]) {
    if (key) {
      affectedKeys.add(key);
    }
  }

  if (affectedKeys.size === 0) {
    return;
  }

  redrawCells(canvas, controller, affectedKeys);
}

export function disposeHeatmap(canvas) {
  const controller = canvas?.__fireChartsHeatmap;
  if (!controller) {
    return;
  }

  canvas.removeEventListener("pointermove", controller.onPointerMove);
  canvas.removeEventListener("pointerleave", controller.onPointerLeave);
  canvas.removeEventListener("click", controller.onClick);
  delete canvas.__fireChartsHeatmap;
}
