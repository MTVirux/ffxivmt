<style>
    .card {
        height: 100%;
    }
    .card-body {
        display: flex;
        flex-direction: column;
        justify-content: space-between;
    }
</style>
<body>
    <div class="container mt-5">
        <div class="row" id="navbar-container">
            <!-- Cards will be injected here -->
        </div>
    </div>

    <script>
        $(document).ready(function() {
            // Assuming the array structure is passed from PHP to JavaScript
            const navbarStructure = <?php echo json_encode($this->config->item('navbar_structure')); ?>;

            const createCard = (name, link, description) => {
                return `
                    <div class="col-md-4 mb-4">
                        <div class="card">
                            <div class="card-body">
                                <h5 class="card-title">${name}</h5>
                                <p class="card-text">${description}</p>
                                <a href="${link}" class="btn btn-primary">Go to ${name}</a>
                            </div>
                        </div>
                    </div>
                `;
            };

            $.each(navbarStructure, function(key, value) {
                if ($.isArray(value)) {
                    $.each(value, function(index, subItem) {
                        $('#navbar-container').append(createCard(subItem.name, subItem.link, subItem.description));
                    });
                } else {
                    $('#navbar-container').append(createCard(value.name, value.link, value.description));
                }
            });
        });
    </script>
</body>
