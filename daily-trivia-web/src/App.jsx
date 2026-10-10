import { useState, useEffect } from 'react'
import './App.css'
import { AuthProvider, useAuth } from './AuthContext';
import AuthForm from './AuthForm';

const API_URL = "http://localhost:5042";

function Quiz() {

const[question, setQuestion] = useState(null);
const[selected, setSelected] = useState(null);
const[result, setResult] = useState(null);
// const[playerName, setPlayerName] = useState('');
const[leaderboard, setLeaderBoard] = useState([]);
const{ token, email, logout } = useAuth();

// Loading today's question
// useEffect(()=>{
//   fetch(`${API_URL}/api/question-of-the-day`).then(res => res.json()).then(setQuestion);
// }, []);

useEffect(() => {
  Promise.all([
    fetch(`${API_URL}/api/question-of-the-day`).then(res => res.json()),
    fetch(`${API_URL}/api/my-status`, {
      headers: { 'Authorization': `Bearer ${token}` }
    }).then(res => res.json())
  ]).then(([questionData, statusData]) => {
    setQuestion(questionData);
    if (statusData.hasPlayed) {
      setResult({ isCorrect: statusData.isCorrect, correctOption: statusData.correctOption });
    }
    fetch(`${API_URL}/api/leaderboard`).then(res => res.json()).then(setLeaderBoard);
  });
}, [token]);

const submitAns = async () =>{
  if(!selected) return;
  
  const res = await fetch(`${API_URL}/api/submit-answer`, {
    method: 'POST',
    headers: {'Content-Type':'application/json', 'Authorization': `Bearer ${token}`},
    body: JSON.stringify({
      questionId: question.id,
      selectedOption:selected
    })
  });

  if(res.status === 401){
    alert("session expired, please login again"),
    logout();
    return;
  }

  const data = await res.json();
  setResult(data);

  const lbRes = await fetch(`${API_URL}/api/leaderboard`);
  setLeaderBoard(await lbRes.json());
};

if (!question) return <p>Loading today's question...</p>;

return (
  <div className="page">
    <div className="card">
      <div className="header-row">
        <h1>Daily Trivia</h1>
        <div className="user-info">
          <span>{email}</span>
          <button className="logout-btn" onClick={logout}>Logout</button>
        </div>
      </div>

      <p className="question-text">{question.text}</p>

      {['A', 'B', 'C', 'D'].map(opt => {

        let className = "option-btn";
        if(result){
          if(opt === result.correctOption) className += ' correct-answer';
          else if(opt === selected) className += ' wrong-answer';
        } else if(selected === opt){
          className += ' selected';
        }

        return (
          <button
            key={opt}
            className={className}
            onClick={() => !result && setSelected(opt)}
            disabled={!!result}
          >
            {question[`option${opt}`]}
          </button>
        );
      })}

      <button className="submit-btn" onClick={submitAns} disabled={!selected || result}>
        Submit Answer
      </button>

      {result && (
        <div className={`result-banner ${result.isCorrect ? 'correct' : 'incorrect'}`}>
          {result.isCorrect ? 'Correct!' : `Wrong — correct answer was ${result.correctOption}`}
        </div>
      )}

      {leaderboard.length > 0 && (
        <div className="leaderboard">
          <h3>Today's Leaderboard</h3>
          <ol>
            {leaderboard.map((entry, i) => (
              <li key={i}><span>{entry.email}</span><span>{entry.correctAnswers}</span></li>
            ))}
          </ol>
        </div>
      )}
    </div>
  </div>
);
}

function AppContent() {
  const { token } = useAuth();
  return token ? <Quiz /> : <AuthForm />;
}

function App() {
  return (
    <AuthProvider>
      <AppContent />
    </AuthProvider>
  );

}

export default App;
