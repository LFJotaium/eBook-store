// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
document.addEventListener("DOMContentLoaded", function () {
    const setDiscountModal = document.getElementById("setDiscountModal");
    const bookIdInput = document.getElementById("bookId");

    setDiscountModal.addEventListener("show.bs.modal", function (event) {
        const button = event.relatedTarget;
        const bookId = button.getAttribute("data-id");
        bookIdInput.value = bookId;
    });
});

