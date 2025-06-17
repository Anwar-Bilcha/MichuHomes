// Smooth scroll
document.querySelectorAll('a[href^="#"]').forEach(anchor => {
    anchor.addEventListener('click', function (e) {
        const targetId = this.getAttribute('href');
        if (targetId.length > 1) {
            e.preventDefault();
            const section = document.querySelector(targetId);
            section?.scrollIntoView({ behavior: 'smooth' });
        }
    });
});

// Search filtering
const searchInput = document.getElementById('propertySearch');
if (searchInput) {
    searchInput.addEventListener('keyup', function () {
        const keyword = this.value.toLowerCase();
        const cards = document.querySelectorAll('#properties .card');
        cards.forEach(card => {
            const name = card.getAttribute('data-name')?.toLowerCase() || '';
            card.parentElement.style.display = name.includes(keyword) ? 'block' : 'none';
        });
    });
}

// Modal logic
function openModal(title, description) {
    document.querySelector('#propertyModal .modal-title').textContent = title;
    document.getElementById('modalContent').innerHTML = `<p>${description}</p>`;
    const modal = new bootstrap.Modal(document.getElementById('propertyModal'));
    modal.show();
}
