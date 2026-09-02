const STORAGE_KEY = "aiHelpersBetaNoticeDismissed";

export function wasDismissed() {
    return localStorage.getItem(STORAGE_KEY) === "true";
}

export function dismiss(dialog) {
    localStorage.setItem(STORAGE_KEY, "true");
    dialog.close();
}

export function show(dialog) {
    dialog.showModal();
}
