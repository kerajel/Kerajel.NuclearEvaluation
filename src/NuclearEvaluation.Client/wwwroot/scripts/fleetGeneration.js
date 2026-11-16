function cellAt(viewport, row, month) {
    return viewport.querySelector(`button[data-row="${row}"][data-month="${month}"]`);
}

export function reveal(viewport, row, month) {
    const cell = cellAt(viewport, row, month);
    if (!cell) return;

    const bounds = cell.getBoundingClientRect();
    const visible = viewport.getBoundingClientRect();
    const headerBottom = cell.closest("table").querySelector("thead th").getBoundingClientRect().bottom;
    const stateRight = cell.closest("tr").querySelector("th").getBoundingClientRect().right;
    const inset = 4;

    if (bounds.top < headerBottom + inset) {
        viewport.scrollTop -= headerBottom + inset - bounds.top;
    } else if (bounds.bottom > visible.bottom - inset) {
        viewport.scrollTop += bounds.bottom - visible.bottom + inset;
    }

    if (bounds.left < stateRight + inset) {
        viewport.scrollLeft -= stateRight + inset - bounds.left;
    } else if (bounds.right > visible.right - inset) {
        viewport.scrollLeft += bounds.right - visible.right + inset;
    }
}

export function listen(viewport) {
    viewport.addEventListener("keydown", event => {
        const cell = event.target.closest("button[data-row][data-month]");
        if (!cell || event.altKey || event.ctrlKey || event.metaKey) return;

        let row = Number(cell.dataset.row);
        let month = Number(cell.dataset.month);
        switch (event.key) {
            case "ArrowUp": row -= 1; break;
            case "ArrowDown": row += 1; break;
            case "ArrowLeft": month -= 1; break;
            case "ArrowRight": month += 1; break;
            case "Home": month = 0; break;
            case "End": month = cell.closest("tr").cells.length - 2; break;
            default: return;
        }

        event.preventDefault();
        const next = cellAt(viewport, row, month);
        if (!next) return;
        next.focus({ preventScroll: true });
        next.click();
    });
}
