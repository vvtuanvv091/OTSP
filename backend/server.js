const express = require("express");
const mongoose = require("mongoose");
const cors = require("cors");

const app = express();
app.use(cors());
app.use(express.json());

// Kết nối MongoDB (BookStoreDB)
mongoose
  .connect("mongodb://127.0.0.1:27017/BookStoreDB")
  .then(() => console.log("Đã kết nối MongoDB"))
  .catch((err) => console.error("Lỗi kết nối:", err));

// Schema + Model (Mongoose tự map model "Book" -> collection "books")
const bookSchema = new mongoose.Schema({
  title: { type: String, required: true },
  author: { type: String, required: true },
  price: { type: Number, required: true },
  isPublished: { type: Boolean, default: true },
});
const Book = mongoose.model("Book", bookSchema);

// [GET] /api/books
app.get("/api/books", async (req, res) => {
  try {
    const books = await Book.find();
    res.json(books);
  } catch (err) {
    res.status(500).json({ message: err.message });
  }
});

// [POST] /api/books
app.post("/api/books", async (req, res) => {
  try {
    const { title, author, price } = req.body;
    const book = await Book.create({ title, author, price });
    res.status(201).json(book);
  } catch (err) {
    res.status(400).json({ message: err.message });-
  }
});

app.listen(5000, () => console.log("Server chạy tại http://localhost:5000"));