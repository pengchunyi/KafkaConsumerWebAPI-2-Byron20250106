// 加載 Sidebar
fetch("sidebar.html")
    .then(response => {
        if (!response.ok) {
            throw new Error("Network response was not ok");
        }
        return response.text();
    })
    .then(data => {
        // 將sidebar.html的內容插入到#sidebar-container
        document.getElementById("sidebar-container").innerHTML = data;

        // 綁定Sidebar按鈕事件
        const toggleButton = document.getElementById("toggle-button");
        const sidebar = document.getElementById("sidebar");

        if (toggleButton && sidebar) {
            toggleButton.addEventListener("click", () => {
                sidebar.classList.toggle("collapsed");
                toggleButton.classList.toggle("collapsed");
            });
        }
    })
    .catch(error => console.error("Error loading sidebar:", error));



//<script>
//        // 加載 Sidebar
//    fetch("sidebar.html")
//            .then(response => {
//                if (!response.ok) {
//                    throw new Error("Network response was not ok");
//                }
//    return response.text();
//            })
//            .then(data => {
//        document.getElementById("sidebar-container").innerHTML = data;

//    // 必須重新綁定事件處理器
//    const toggleButton = document.getElementById("toggle-button");
//    const sidebar = document.getElementById("sidebar");
//    const content = document.querySelector(".content");

//                toggleButton.addEventListener("click", () => {
//        sidebar.classList.toggle("collapsed");
//    content.classList.toggle("collapsed");
//    toggleButton.innerHTML = sidebar.classList.contains("collapsed") ? "&gt;" : "&lt;";
//                });
//            })
//            .catch(error => console.error("Error loading sidebar:", error));
//</script>