import torch
import torch.nn as nn
import torch.nn.functional as F

# hyperparameters
learning_rate = 0.01
epochs = 10000
eval_interval = 1000
eval_iters = 200
batch_size = 8
device = 'cuda' if torch.cuda.is_available() else 'cpu'
# Training data columns: 0-level, 1:20-voltorb numbers, 21:45-known board state
# What we want to predict: 46:70-full board state
input_size = 46
hidden_size = 6000
output_size = 25

# Read training data
with open('training_data.csv', 'r') as f:
    data_text = f.read()

# Tokenize data
data_text = data_text.split('\n')
# final row is empty so we remove it
full_data = [list(map(lambda e: 0 if e == '' else float(e), data_row.split('\t'))) for data_row in data_text][:-1]

# Convert data to tensors
full_data = torch.tensor(full_data, dtype=torch.float32, device=device)

# MinMax normalization on each column of data
level = full_data[:, 0:1]
volt = full_data[:, 1:21]
known = full_data[:, 21:46]
full = full_data[:, 46:71]
level = (level - level.min()) / (level.max() - level.min())
volt = (volt - volt.min()) / (volt.max() - volt.min())
known = (known - known.min()) / (known.max() - known.min())
full = (full - full.min()) / (full.max() - full.min())
full_data = torch.cat((level, volt, known, full), dim=-1)

# Split data into train and test
n = int(0.9 * len(full_data))
training_data = full_data[:n]
test_data = full_data[n:]

# Model
class Model(nn.Module):
    def __init__(self):
        super().__init__()
        self.l1 = nn.Linear(input_size, hidden_size)
        self.l2 = nn.Linear(hidden_size, output_size)
        self.softmax = nn.Softmax(dim=1)
    
    def forward(self, x, y):
        # TODO: make better :)
        x = self.l1(x)
        x = self.l2(x)

        # Find loss
        loss = F.l1_loss(x, y)
        return x, loss

model = Model()
m = model.to(device)
# Optimizer
optimizer = torch.optim.SGD(m.parameters(), lr=learning_rate)

# Get random batch from data
def get_batch(split):
    data = training_data if split == 'train' else test_data
    random_nums = torch.randint(len(data), (batch_size,)) 
    batch = data[random_nums] # (batch_size, 71)
    x = batch[:, :input_size] # (batch_size, 46)
    y = batch[:, input_size:] # (batch_size, 25)
    return x, y

# Evaluate loss
@torch.no_grad()
def estimate_loss():
    out = {}
    model.eval()
    for split in ['train', 'val']:
        losses = torch.zeros(eval_iters)
        for k in range(eval_iters):
            x, y = get_batch(split)
            _, loss = m(x, y)
            losses[k] = loss.item()
        out[split] = losses.mean()
    model.train()
    return out

# Training
for epoch in range(epochs):
    # Evaluate model on train and test data
    if epoch % eval_interval == 0:
        loss = estimate_loss()
        print(f'step {epoch}: train loss {loss["train"]}, val loss {loss["val"]}')
        pass
    
    # Get random batch of data
    x, y = get_batch('train')

    # Evaluate loss
    _, loss = m(x, y)
    optimizer.zero_grad(set_to_none=True)
    loss.backward()
    optimizer.step()