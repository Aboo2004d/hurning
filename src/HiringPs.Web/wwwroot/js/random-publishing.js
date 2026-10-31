"use strict";
document.querySelectorAll("[data-random-schedule]").forEach(form => {
    const count = form.querySelector("[data-random-count]");
    const container = form.querySelector("[data-random-times]");
    const values = Array.from(container.querySelectorAll("input")).map(input => input.value);
    const render = () => {
        const n = Number(count.value);
        if (!Number.isInteger(n) || n < 1 || n > 24) return;
        container.querySelectorAll("input").forEach((input, i) => { values[i] = input.value; });
        container.replaceChildren();
        for (let i = 0; i < n; i++) {
            const label = document.createElement("label");
            label.append(document.createTextNode(`${form.dataset.slotLabel} ${i + 1}`));
            const input = document.createElement("input");
            input.type = "time"; input.name = `Times[${i}]`; input.step = "60"; input.required = true;
            if (!values[i]) {
                const previous = (values[i - 1] || "08:55").split(":").map(Number);
                const minutes = (previous[0] * 60 + previous[1] + 5) % 1440;
                values[i] = `${String(Math.floor(minutes / 60)).padStart(2, "0")}:${String(minutes % 60).padStart(2, "0")}`;
            }
            input.value = values[i]; label.append(input); container.append(label);
        }
    };
    count.addEventListener("input", render);
});
