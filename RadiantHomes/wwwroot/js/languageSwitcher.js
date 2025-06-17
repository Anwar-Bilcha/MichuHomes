//<script type="text/javascript">
//    // Function to load the language file
//    function loadLanguage(language) {
//        // Fetch the language file from the server
//        fetch(`/locales/${language}.json`)
//            .then(response => {
//                if (!response.ok) {
//                    throw new Error(`Failed to load language file for ${language}`);
//                }
//                return response.json();
//            })
//            .then(data => {
//                // Iterate through all translation keys in the loaded language data
//                for (const key in data) {
//                    if (data.hasOwnProperty(key)) {
//                        const elements = document.querySelectorAll(`[data-translate="${key}"]`);
//                        elements.forEach(element => {
//                            // If the key is found, set the translated text
//                            element.innerText = data[key] || element.innerText;
//                        });
//                    }
//                }
//            })
//            .catch(error => {
//                console.error("Error loading language file:", error);
//            });
//    }

//    // Function to get the user's preferred language or default to English
//    function getUserLanguage() {
//        let userLang = navigator.language || 'en'; // Default to 'en' if not found
//    // Normalize to the first two characters (e.g., 'en', 'am', 'fr')
//    return userLang.slice(0, 2).toLowerCase();
//    }

//    // Initialize language loading when the DOM is ready
//    document.addEventListener('DOMContentLoaded', function () {
//        const userLang = getUserLanguage();
//    loadLanguage(userLang); // Load the language file based on user's preference
//    });
//</script>



<script type="text/javascript">
    async function loadLanguage(language) {
        try {
            // Load static translations from JSON
            const [staticRes, dbRes] = await Promise.all([
    fetch(`/locales/${language}.json`),
    fetch(`/api/localization/${language}`)
    ]);

    const staticTranslations = await staticRes.json();
    const dbTranslations = await dbRes.json();

    // Merge both translations
    const mergedTranslations = {
        ...staticTranslations,
                ...dbTranslations // DB keys will override JSON if there's overlap
            };

    // Apply translations
    for (const key in mergedTranslations) {
                const elements = document.querySelectorAll(`[data-translate="${key}"]`);
                elements.forEach(element => {
        element.innerText = mergedTranslations[key] || element.innerText;
                });
            }
        } catch (error) {
        console.error("Error loading translations:", error);
        }
    }

    function getUserLanguage() {
        let userLang = navigator.language || 'am';
    return userLang.slice(0, 2).toLowerCase();
    }

    document.addEventListener('DOMContentLoaded', function () {
        const userLang = getUserLanguage();
    loadLanguage(userLang);
    });
</script>
