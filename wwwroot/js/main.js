
// to show images on upload
document.addEventListener("DOMContentLoaded", function () {
    // Function to display the uploaded image for elements with class "image-upload"
    function displayImageForUpload(input) {
        var image = input.closest('.form-group').querySelector('img'); // Get the closest img element in the same form-group

        if (input.files.length > 0) {
            var file = input.files[0];
            var reader = new FileReader();

            reader.onload = function (e) {
                image.src = e.target.result;
                image.style.display = "block";
            };

            reader.readAsDataURL(file);
        } else {
            image.src = "";
            image.style.display = "none";
        }
    }

    // Attach the change event to input elements with class "image-upload"
    var imageUploadInputs = document.querySelectorAll('.image-upload');
    imageUploadInputs.forEach(function (input) {
        input.addEventListener('change', function () {
            displayImageForUpload(input);
        });
    });
});




