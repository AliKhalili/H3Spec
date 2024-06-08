const express = require('express')
const app = express()
const port = 3000

app.get('/echo', (req, res) => {
    var data = req.query;
    res.send(data)
})

app.post('/echo', (req, res) => {
    var data = req.body;
    res.send(data)
})

app.listen(port, () => {
    console.log(`dummy app listening on port ${port}`)
})