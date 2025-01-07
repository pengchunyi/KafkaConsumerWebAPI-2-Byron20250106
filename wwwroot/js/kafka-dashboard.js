// 全局數據緩存
let cachedMessages = [];

// 初始化緩存數據
function initializeCachedMessages() {
    const savedMessages = localStorage.getItem("kafkaMessages");
    if (savedMessages) {
        cachedMessages = JSON.parse(savedMessages);
        updateMessageList();
    }
}

// 保存數據到 localStorage
function saveMessagesToLocalStorage() {
    localStorage.setItem("kafkaMessages", JSON.stringify(cachedMessages));
}

// 更新消息列表
function updateMessageList() {
    const messageList = document.getElementById("messageList");
    messageList.innerHTML = ""; // 清空列表

    if (cachedMessages.length === 0) {
        // 當沒有數據時顯示提示
        const li = document.createElement("li");
        li.textContent = "暫無數據";
        li.className = "loading";
        messageList.appendChild(li);
    } else {
        // 顯示緩存數據
        cachedMessages.forEach(message => {
            const li = document.createElement("li");
            li.textContent = message;
            messageList.appendChild(li);
        });
    }
}

// 連接 SignalR Hub
const connection = new signalR.HubConnectionBuilder()
    .withUrl("http://localhost:5000/messageHub")
    .build();

connection.on("ReceiveMessage", (user, message) => {
    const newMessage = `${user}: ${message}`;
    cachedMessages.push(newMessage);
    saveMessagesToLocalStorage(); // 保存數據
    updateMessageList(); // 更新顯示
});

connection.start().catch(err => console.error("SignalR Error: ", err));

// 定期從後端拉取 Kafka 緩存消息
async function fetchMessages() {
    try {
        const response = await fetch("http://localhost:5000/api/messages");
        const messages = await response.json();

        // 如果有新數據則更新緩存
        if (JSON.stringify(messages) !== JSON.stringify(cachedMessages)) {
            cachedMessages = messages;
            saveMessagesToLocalStorage(); // 保存數據
            updateMessageList(); // 更新顯示
        }
    } catch (error) {
        console.error("Fetch Error:", error);
    }
}

// 初始化：顯示緩存或拉取數據
document.addEventListener("DOMContentLoaded", () => {
    initializeCachedMessages(); // 初始化緩存數據
    fetchMessages();
});

setInterval(fetchMessages, 2000); // 每 2 秒拉取一次數據
